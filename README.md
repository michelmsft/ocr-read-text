# Step 0: Download Install Cosmos DB for NoSQL Emulator
for the sake of this example, we are going to use a local Cosmos DB for NoSQL Emulator as our backend DB.

Go to the official download page: https://aka.ms/cosmosdb-emulator go to https://localhost:8081/_explorer/index.html and make sure it up running. in your local cosmos db emulator, you will create the following components:

1. Create a database named dmvdb
2. Create a container named registry with /plateNumber as partition key.
