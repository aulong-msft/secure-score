using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.SecurityCenter;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

public static class SecureScoreFunctions
{
    // Global Variables
    private static IConfiguration? configuration;
    private const string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
    private const int DEFAULT_SCORE_PERCENTAGE = 100;

    public static async Task Main(string[] args)
    {
        configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        string filePath = configuration["AppSettings:Output_FilePath"] ?? string.Empty;
        if (filePath == null)
        {
            throw new Exception("Error: The output file path is not specified in the configuration.");

        }

        string azFilePath = configuration["AppSettings:Az_FilePath"] ?? string.Empty;      
        if (azFilePath == null)
        {
            throw new Exception("Error: The azFilePath is not specified in the configuration.");

        }

        // Perform an Az Login
        AzHelpers.AzLogin(azFilePath);

        // Check if the file exists at the specified filePath
        if (!File.Exists(filePath))
        {
            var secureScoreJson = await CreateSecureScoreJson(configuration);

            // Convert the single json document to a list of json documents
            var singleJsonDocumentList = new List<SecureScoreResult> { secureScoreJson };

            // Write to disk
            var outputFilePath = await WriteSecureScoreJsonToFile(singleJsonDocumentList, filePath);

            Console.WriteLine("Output File Path: " + outputFilePath);
            Console.WriteLine($"Secure score JSON file generated at: {outputFilePath}");
        }
        else
        {
            // Read in the json file and add the json documents to the list
            List<SecureScoreResult> jsonDocumentsList = ProcessJsonFile(filePath);
            
            // Save the current instance of currentScore
            var lastResult = jsonDocumentsList.Last();
            if (lastResult is null || lastResult.currentScore == null)
            {
                throw new Exception("Error: Could not retrieve the secure score data.");
            }
            
            var currentScore = lastResult.currentScore.Value;

            // Call to get the new secure sore
            var secureScoreJson = await CreateSecureScoreJson(configuration);
            if (secureScoreJson != null)
            {
                jsonDocumentsList.Add(secureScoreJson);
                var newScore = secureScoreJson.currentScore;
                if (newScore == null)
                {
                    throw new Exception("Error: Could not retrieve the secure score data.");
                }
                // Calculate the delta between the scores
                var deltaScore = (double) newScore - currentScore;
                Console.WriteLine("Delta Score: " + deltaScore + " (New Score: " + newScore + ", Current Score: " + currentScore + ")");

                // Output delta score information
                OutputDeltaScoreInformation(deltaScore);

                // Write the new file to disk
                await WriteSecureScoreJsonToFile(jsonDocumentsList, filePath);
            }
            else
            {
                Console.WriteLine("Error: Could not retrieve the secure score data from Azure.");
            }
        }
    }

    // The GetSecureScoreData function retrieves secure score data from Azure using the Azure SDK. 
    // It authenticates with Azure using the Azure Identity library, fetches the secure score data using the ArmClient, 
    // and returns the secure score data.
    public static async Task<SecureScoreData> GetSecureScoreData(IConfiguration configuration)
    {
        Console.WriteLine("Getting secure score data from Azure...");
        var subscriptionId = configuration["AppSettings:Subscription_ID"];
        var secureScoreName = configuration["AppSettings:SecureScoreName"]; 

        // Check to makesure subscriptionID is a valid guid 
        Guid subscriptionGuid;
        if (!Guid.TryParse(subscriptionId, out subscriptionGuid))
        {
            throw new InvalidOperationException("Invalid Subscription_ID. Please check the appsettings.json file.");
        }

        // Get Azure credential
        TokenCredential cred = new DefaultAzureCredential();
        ArmClient client = new ArmClient(cred);

        ResourceIdentifier secureScoreResourceId = SecureScoreResource.CreateResourceIdentifier(subscriptionId, secureScoreName);
        SecureScoreResource secureScore = client.GetSecureScoreResource(secureScoreResourceId);
        SecureScoreResource result = await secureScore.GetAsync();
        SecureScoreData resourceData = result.Data;

        return resourceData;
    }

    // The CreateSecureScoreJson function prepares a new secure score JSON file. 
    // It retrieves secure score data using the GetSecureScoreData function, calculates the score percentage,
    // and creates a JSON object containing the score information. It then serializes the object into a JSON string,
    // parses it into a JsonDocument, and returns the document.
    public static async Task<SecureScoreResult> CreateSecureScoreJson(IConfiguration configuration)
    {
        
        Console.WriteLine("Prepaping a new secure score JSON file...");
        var secureScoreData = await GetSecureScoreData(configuration);
        if (secureScoreData == null)
        {
            throw new Exception("Error: Could not retrieve the secure score data from Azure.");
        }

        var secureScoreResult = new SecureScoreResult()
        {
            scorePercentage = Convert.ToInt32((secureScoreData.Current / secureScoreData.Max) * DEFAULT_SCORE_PERCENTAGE),
            currentScore = secureScoreData.Current,
            maxScore = secureScoreData.Max,
            scoreName = secureScoreData.Name,
            subId = secureScoreData.Id.SubscriptionId,
            formattedDateTime = DateTime.Now.ToString(DATE_TIME_FORMAT)
        };

        return secureScoreResult;
    }

    //The WriteSecureScoreJsonToFile function takes in a list of SecureScoreResult objects 
    // and a file output path as parameters. It is defined as an asynchronous task and returns a string representing the path of the output file.
   public static async Task<string> WriteSecureScoreJsonToFile(List<SecureScoreResult> jsonDocuments, string outputPath)
    {
        Console.WriteLine("Writing a new secure score JSON file to " + outputPath);
        
        var secureScoreJson = JsonSerializer.Serialize(jsonDocuments);
        await File.WriteAllTextAsync(outputPath, secureScoreJson);
        return outputPath;
    }

    // The ProcessJsonFile function reads and processes an existing JSON file, validating its structure and parsing individual JSON documents
    // into JsonDocument objects. It returns an array of JsonDocument representing the parsed JSON documents.
    public static List<SecureScoreResult> ProcessJsonFile(string filePath)
    {
        try
        {
            string jsonData = File.ReadAllText(filePath);
            if (jsonData == null)
            {
                // File read failed or the file is empty
                throw new Exception("File read operation failed or file is empty.");
            }

            List<SecureScoreResult>? secureScoreResults = JsonSerializer.Deserialize<List<SecureScoreResult>>(jsonData);
            if (secureScoreResults == null)
            {
 
                throw new InvalidOperationException("Deserialization failed or the JSON data is invalid");

            }
            Console.WriteLine("JSON documents successfully parsed:");
            return secureScoreResults;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error parsing JSON: " + ex.Message);
            Console.WriteLine("Please make sure you provide a valid JSON file.");
            throw new InvalidOperationException("There was an error processing this file, please try again" + ex.Message);
        }
    }

    // The OutputDeltaScoreInformation function takes a deltaScore of type double as input and provides
    // an output message based on the value of the deltaScore. Its purpose is to display information about the change in secure score.
    public static void OutputDeltaScoreInformation(double deltaScore)
    {
        if (deltaScore == 0)
        {
            Console.WriteLine("No change in secure score! " + deltaScore);
        }
        else if (deltaScore > 0)

        {
            Console.WriteLine("Your secure score increased!! " + deltaScore);
        }
        else
        {
            Console.WriteLine("Your secure score decreased " + deltaScore);
        }
    }
}