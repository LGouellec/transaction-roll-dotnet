#!/bin/bash

if [ $# -lt 2 ]; then
    echo "No arguments provided - ./start.sh <TOPIC-NAME> <TRANSACTION-ID>"
    exit 1
fi

cp docker-compose-template.yml docker-compose.yml
sed -i '' "s/TOPIC_TRANSACTION_TO_REPLACE/$1/" docker-compose.yml
sed -i '' "s/TRANSACTION_ID_TO_REPLACE/$2/" docker-compose.yml

# Generate admin cli tools
cd admin-cli-tools
mvn clean package
cp target/admin-cli-tools-1.0-SNAPSHOT-jar-with-dependencies.jar ../admin-cli-tools.jar
cd ..

# Build localy docker-compose images
docker-compose build

# Start kafka brokers & zookeeper
docker-compose up -d zookeeper-1 zookeeper-2 zookeeper-3 kafka-1 kafka-2 kafka-3
zookeeperContainerId=`docker ps -f name=zookeeper-1 | tail -n 1 | awk '{print $1}'`
kafkaContainerId=`docker ps -f name=kafka-1 | tail -n 1 | awk '{print $1}'`

# Wait zookeeper-1 is UP
test=true
while test
do
    ret=`echo ruok | docker exec -i ${zookeeperContainerId} nc localhost 2181 | awk '{print $1}'`
    sleep 1
    echo "Waiting zookeeper UP"
    if $ret == 'imok'
    then
        test=false
    fi
done

# Wait brokers is UP
test=true
while test
do
    ret=`echo dump | docker exec -i ${zookeeperContainerId} nc localhost 2181 | grep brokers | wc -l`
    sleep 1
    echo "Waiting kafka UP"
    if $ret == 3
    then
        test=false
    fi
done

# Create topic which use by producer & consumer
docker exec -i ${kafkaContainerId} kafka-topics --bootstrap-server kafka-1:9092 --topic $1 --create --partitions 4 --replication-factor 3 > /dev/null 2>&1
echo "Topic $1 created"

docker-compose up -d producer consumer

previousBrokerDown=""
stopBroker=false
i=0
while true
do
    brokerId=`java -jar admin-cli-tools.jar localhost:19092,localhost:19093,localhost:19094 | jq -r  --arg TRANSACTION_ID "$2" '.[] | select(.transactionId==$TRANSACTION_ID and (.transactionState=="Ongoing" or .transactionState=="PrepareCommit")) | .brokerId'`
    if $stopBroker
    then
        ((i=i+1))
        if [ $i -eq 10 ]
        then
            echo "Restart kafka container ${previousBrokerDown}"
            docker restart $previousBrokerDown
            previousBrokerDown=""
        elif [ $i -ge 20 ]
        then 
            echo $"Iteration done, maybe an another broker will crash soon ..."
            i=0  
            stopBroker=false 
            sleep 10
        fi
    fi

    if [[ ! -z "$brokerId" ]] && [[ $stopBroker == false ]]
    then
        echo "Broker id ${brokerId} is the leader for the transaction $2"
        brokerIdContainer=`docker ps -f name=kafka-${brokerId} | tail -n 1 | awk '{print $1}'`
        echo $"Stop broker container ${brokerIdContainer}"
        previousBrokerDown=${brokerIdContainer}
        docker stop $brokerIdContainer
        stopBroker=true
    fi
    sleep 1
done
    