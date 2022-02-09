package org.example.admin;

import org.apache.kafka.clients.admin.TransactionListing;

public class TransactionMapper {
    public static  class Transaction{
        private final Integer brokerId;
        private final String transactionId;
        private final long producerId;
        private final String transactionState;


        public Transaction(String transactionId, long producerId, String transactionState, Integer brokerId) {
            this.transactionId = transactionId;
            this.producerId = producerId;
            this.transactionState = transactionState;
            this.brokerId = brokerId;
        }

        public String getTransactionId() {
            return transactionId;
        }

        public long getProducerId() {
            return producerId;
        }

        public String getTransactionState() {
            return transactionState;
        }

        public Integer getBrokerId() {
            return brokerId;
        }
    }

    public static Transaction map(TransactionListing transactionListing, Integer brokerId){
        return new Transaction(
                transactionListing.transactionalId(),
                transactionListing.producerId(),
                transactionListing.state().toString(),
                brokerId);
    }
}
