using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

// Import namespaces
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace read_text
{
    class Program
    {
        private static ComputerVisionClient cvClient;

        static async Task Main(string[] args)
        {
            try
            {
                // Load configuration settings
                IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
                IConfigurationRoot configuration = builder.Build();
                string cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
                string cogSvcKey = configuration["CognitiveServiceKey"];

                // Initialize Azure Computer Vision client
                ApiKeyServiceClientCredentials credentials = new ApiKeyServiceClientCredentials(cogSvcKey);
                cvClient = new ComputerVisionClient(credentials)
                {
                    Endpoint = cogSvcEndpoint
                };

                while (true)
                {
                    // User prompt
                    Console.WriteLine("\nPress 1 or 2 or 3 to take a picture and analyze it.");
                    Console.WriteLine("Press any other key to quit.");
                    string command = Console.ReadLine();

                    string imageFile;
                    string plateNumber;

                    switch (command)
                    {
                        case "1":
                            imageFile = "images/plate-1.png";
                            plateNumber = await GetTextRead(imageFile);
                            await GetPlate(plateNumber);
                            break;
                        case "2":
                            imageFile = "images/plate-2.png";
                            plateNumber = await GetTextRead(imageFile);
                            await GetPlate(plateNumber);
                            break;
                        case "3":
                            imageFile = "images/plate-3.png";
                            plateNumber = await GetTextRead(imageFile);
                            await GetPlate(plateNumber);
                            break;
                        default:
                            Console.WriteLine("Exiting...");
                            return;
                    }
                }

                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static async Task<string> GetTextRead(string imageFile)
        {
            Console.WriteLine($"Reading text in {imageFile}\n");
            using (var imageData = File.OpenRead(imageFile))
            {
                var readOp = await cvClient.ReadInStreamAsync(imageData);

                // Get the async operation ID so we can check for the results
                string operationLocation = readOp.OperationLocation;
                string operationId = operationLocation.Substring(operationLocation.Length - 36);

                // Wait for the asynchronous operation to complete
                ReadOperationResult results;
                do
                {
                    Thread.Sleep(1000);
                    results = await cvClient.GetReadResultAsync(Guid.Parse(operationId));
                }
                while ((results.Status == OperationStatusCodes.Running ||
                        results.Status == OperationStatusCodes.NotStarted));

                // If the operation was successful, process the text line by line
                if (results.Status == OperationStatusCodes.Succeeded)
                {
                    var textUrlFileResults = results.AnalyzeResult.ReadResults;
                    foreach (ReadResult page in textUrlFileResults)
                    {
                        foreach (Line line in page.Lines)
                        {
                            Console.WriteLine(line.Text);
                            string cleanedText = Regex.Replace(line.Text.ToUpper(), "[^A-Z0-9]", "");
                            if (cleanedText != "TEXAS")
                            {
                                // return the first detected line as plate number
                                return cleanedText;
                            }

                        }
                    }
                    return "UNKNOWN";
                }
            }

            return null;
        }

        static async Task<string> GetPlate(string plateNumber)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();

            // Initialize Cosmos DB client and container
            string endpointUri = configuration["endpointUri"];
            string primaryKey = configuration["primaryKey"];

            var vehicleRegistrationService = new VehicleRegistrationService(endpointUri, primaryKey);

            // Get registration details by plate number
            var registrationDetails = await vehicleRegistrationService.GetRegistrationByPlateNumberAsync(plateNumber);
            if (registrationDetails != null)
            {
                var markdown = new StringBuilder();
                markdown.AppendLine("| Field | Value |");
                markdown.AppendLine("|-------|-------|");
                markdown.AppendLine($"| ID | {registrationDetails.Id} |");
                markdown.AppendLine($"| Plate Number | {registrationDetails.PlateNumber} |");
                markdown.AppendLine($"| Owner Name | {registrationDetails.OwnerName} |");
                markdown.AppendLine($"| Vehicle Type | {registrationDetails.VehicleType} |");
                markdown.AppendLine($"| Make | {registrationDetails.Make} |");
                markdown.AppendLine($"| Model | {registrationDetails.Model} |");
                markdown.AppendLine($"| Year | {registrationDetails.Year} |");
                markdown.AppendLine($"| Registration Date | {registrationDetails.RegistrationDate:yyyy-MM-dd} |");
                markdown.AppendLine($"| Expiration Date | {registrationDetails.ExpirationDate:yyyy-MM-dd} |");
                markdown.AppendLine($"| State | {registrationDetails.State} |");

                Console.WriteLine(markdown.ToString());
                return markdown.ToString();
            }
            else
            {
                var notFoundMessage = $"| Message | No registration found for plate number `{plateNumber}` |";
                Console.WriteLine(notFoundMessage);
                return notFoundMessage;
            }
        }
    }

    public class VehicleRegistrationService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public VehicleRegistrationService(string endpointUri, string primaryKey)
        {
            _cosmosClient = new CosmosClient(endpointUri, primaryKey);
            _container = _cosmosClient.GetContainer("db82875", "registry");
        }

        public async Task<VehicleRegistration> GetRegistrationByPlateNumberAsync(string plateNumber)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.plateNumber = @plateNumber")
                            .WithParameter("@plateNumber", plateNumber);

            using var iterator = _container.GetItemQueryIterator<VehicleRegistration>(query);
            while (iterator.HasMoreResults)
            {
                foreach (var item in await iterator.ReadNextAsync())
                {
                    return item;
                }
            }

            return null;
        }
    }

    public class VehicleRegistration
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("plateNumber")]
        public string PlateNumber { get; set; }

        [JsonProperty("ownerName")]
        public string OwnerName { get; set; }

        [JsonProperty("vehicleType")]
        public string VehicleType { get; set; }

        [JsonProperty("make")]
        public string Make { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("registrationDate")]
        public DateTime RegistrationDate { get; set; }

        [JsonProperty("expirationDate")]
        public DateTime ExpirationDate { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }
    }
}
