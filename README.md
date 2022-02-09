# What happens when a cluster roll during a transaction pending ?

## Require

- maven
- jq
- docker & docker-compose
- bash version >= 5

## Start 
``` bash
./start.sh
```

Open a new terminal :
``` bash
docker-compose logs -f producer
```

## Stop
``` bash
./stop.sh
```