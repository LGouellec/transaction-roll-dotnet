package org.example.admin;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.kafka.clients.admin.AdminClient;
import org.apache.kafka.clients.admin.AdminClientConfig;
import org.apache.kafka.clients.admin.TransactionListing;

import java.util.*;
import java.util.concurrent.ExecutionException;
import java.util.stream.Collectors;

public class Main {

    public static void main(String[] args) throws ExecutionException, InterruptedException {

        if(args.length > 0) {

            Properties properties = new Properties();
            properties.put(AdminClientConfig.BOOTSTRAP_SERVERS_CONFIG, args[0]);
            AdminClient c = AdminClient.create(properties);
            Map<Integer, Collection<TransactionListing>> mapTransactions = new HashMap<>();

            try {
                mapTransactions = c.listTransactions().allByBrokerId().get();
            }catch(Exception e){ /* NOTHING */ }

            ObjectMapper mapper = new ObjectMapper();
            try {
                List<TransactionMapper.Transaction> transactions = new ArrayList<>();

                for(Map.Entry<Integer, Collection<TransactionListing>> entry :  mapTransactions.entrySet())
                    transactions.addAll(entry.getValue().stream().map(t -> TransactionMapper.map(t, entry.getKey())).collect(Collectors.toList()));

                System.out.println(mapper.writeValueAsString(transactions));
            } catch (JsonProcessingException e) {
                System.out.println("ERROR JSON");
            }
        }
        else
            System.out.println("Error - need brokers url in argument");
    }
}
