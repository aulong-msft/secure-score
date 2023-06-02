using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.SecurityCenter;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

public static class SecureScoreFunctions
{
    // Global variables
    private static IConfiguration? configuration;
    private const string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
    private const int DEFAULT_SCORE_PERCENTAGE = 100;
    private const int ZERO = 0;
    private const string OUTPUT_PATH = "secure-score.json";
    public static async Task Main(string[] args)
{
    configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();

    // Prompt the user to fill out the subsctipionID and do an az login before continuing
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("WARNING: Please make sure you have filled out the Subscription_ID and login with az login in the appsettings.json file before continuing.");
    Console.ResetColor();

    //get user input to a json filepath and sa5ve it to the vairable filePath
    //ask the user if they want to process a new secure score, or build one over time
    string userChoice;

    while (true)
    {
        Console.WriteLine("Would you like to process a new secure score, or build one over time? (new/build)");
        userChoice = Console.ReadLine();

        // Check if the entered choice is valid
        if (!string.IsNullOrWhiteSpace(userChoice) && (userChoice.ToLower() == "new" || userChoice.ToLower() == "build"))
        {
            break; // Exit the loop if a valid choice is entered
        }
        else
        {
            Console.WriteLine("Invalid choice. Please enter 'new' or 'build'.");
        }
    }

    if (userChoice == "new")
    {
        var secureScoreJson = await CreateSecureScoreJson(configuration);
        var outputFilePath = await WriteSecureScoreJsonToFile(secureScoreJson, OUTPUT_PATH);
        string jsonString = JsonSerializer.Serialize(secureScoreJson.RootElement, new JsonSerializerOptions { WriteIndented = true });

        Console.WriteLine("Output File Path: " + outputFilePath);
        Console.WriteLine($"Secure score JSON file generated at: {outputFilePath}");
    }

    else if (userChoice == "build")
    {
        string filePath;

        while (true)
        {
            Console.WriteLine("Please enter the path to the JSON file you would like to process:");
            filePath = Console.ReadLine();

            // Check if the entered path is valid
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                break; // Exit the loop if a valid path is entered
            }
            else
            {
                Console.WriteLine("Invalid file path. Please try again.");
            }
        }

        JsonDocument jsonDocument = ProcessJsonFile(filePath);

        if (jsonDocument != null)
        {
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
            {
                JsonElement root = jsonDocument.RootElement;
                ValidateJsonObject(root);
                await WriteJsonDocumentToFile(jsonDocument, filePath);
            }
            else if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
            {
                JsonElement root = jsonDocument.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in root.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.Object)
                        {
                            ValidateJsonObject(element);
                        }
                    }
                    await WriteJsonDocumentToFile(jsonDocument, filePath);
                }
            }
        }
        else
        {
            Console.WriteLine("Failed to process the JSON document.");
        }
    }
    else
    {
        Console.WriteLine("Please enter a valid option.");
    }
 
}
    // This function retrieves secure score data from Azure using the Azure SDK.
    // It takes the Azure subscription ID and secure score name from the configuration,
    // authenticates using the Azure Identity library, and fetches the secure score data using the ArmClient.
    public static async Task<SecureScoreData> GetSecureScoreData(IConfiguration configuration)
    {
        Console.WriteLine("Getting secure score data from Azure...");
        var subscriptionId = configuration["AppSettings:Subscription_ID"];
        var secureScoreName = configuration["AppSettings:SecureScoreName"]; 

        //check to makesure subscriptionID is a valid guid 
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

    // This function retrieves secure score data from Azure and generates a JSON representation of the secure score. 
    // It uses the Azure SDK and the Azure Identity library to authenticate and interact with Azure resources.
    public static async Task<JsonDocument> CreateSecureScoreJson(IConfiguration configuration)
    {
        Console.WriteLine("Prepaping a new secure score JSON file...");
        var secureScoreData = await GetSecureScoreData(configuration);
        var currentScore = secureScoreData.Current;
        var maxScore = secureScoreData.Max;
        var scorePercentage = Convert.ToInt32((currentScore / maxScore) * DEFAULT_SCORE_PERCENTAGE);
        var scoreName = secureScoreData.Name;
        var subId = secureScoreData.Id.SubscriptionId;

        DateTime currentDateTime = DateTime.Now;
        string formattedDateTime = currentDateTime.ToString(DATE_TIME_FORMAT);

        var secureScoreObject = new
        {
            scorePercentage,
            currentScore,
            maxScore,
            scoreName,
            subId,
            formattedDateTime
        };

        var secureScoreJson = JsonSerializer.Serialize(secureScoreObject);
        var jsonDocument = JsonDocument.Parse(secureScoreJson);

        return jsonDocument;
    }

   // This function takes the generated secure score JSON and writes it to a file 
   // specified by the outputPath parameter.
    public static async Task<string> WriteSecureScoreJsonToFile(object jsonDocuments, string outputPath)
    {
        Console.WriteLine("Writing a new secure score JSON file to " + outputPath);
        string secureScoreJson;
        
        if (jsonDocuments is JsonDocument singleDocument)
        {
            secureScoreJson = singleDocument.RootElement.GetRawText();
        }
        else if (jsonDocuments is List<JsonDocument> documentList)
        {
            var jsonElements = documentList.Select(doc => doc.RootElement);
            secureScoreJson = JsonSerializer.Serialize(jsonElements);
        }
        else
        {
            throw new ArgumentException("Invalid argument type. Expected JsonDocument or List<JsonDocument>.");
        }

        await File.WriteAllTextAsync(outputPath, secureScoreJson);
        return outputPath;
    }

    // This function reads and processes an existing JSON file containing secure score data. 
    // It validates the JSON structure, validates individual JSON objects, and writes the processed JSON document back to the same file.
    public static JsonDocument ProcessJsonFile(string filePath)
    {
        try
        {
            string jsonData = File.ReadAllText(filePath);
            Console.WriteLine("JSON file successfully read:");
            JsonDocumentOptions options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };

            JsonDocument jsonDocument = JsonDocument.Parse(jsonData, options);
            // Process the JSON object or array here
            Console.WriteLine("JSON object successfully parsed:");

            if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Access the properties of the object
                JsonElement root = jsonDocument.RootElement;
                ValidateJsonObject(root);
            }
            else if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Access the elements of the array
                JsonElement root = jsonDocument.RootElement;
                foreach (JsonElement element in root.EnumerateArray())
                {
                    ValidateJsonObject(element);
                }
            }
            else
            {
                Console.WriteLine("Invalid JSON structure. Expected an object or an array at the root level.");
                throw new InvalidOperationException("Invalid JSON structure.");
            }

            return jsonDocument;
        }
        catch (JsonException ex)
        {
            Console.WriteLine("Error parsing JSON: " + ex.Message);
            Console.WriteLine("Please make sure you provide a valid JSON file.");
        }

        throw new InvalidOperationException("There was an error processing this file, please try again");
    }

    // This function validates an individual JSON object representing a secure score. 
    // It checks for the presence of required properties and performs basic data validation.
    private static void ValidateJsonObject(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            JsonElement scorePercentage = jsonElement.GetProperty("scorePercentage");
            JsonElement currentScore = jsonElement.GetProperty("currentScore");
        }
        else if (jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement nestedElement in jsonElement.EnumerateArray())
            {
                if (nestedElement.ValueKind == JsonValueKind.Object)
                {
                    ValidateJsonObject(nestedElement);
                }

            }
        }
        else
        {
            Console.WriteLine("Invalid JSON structure. Expected an object or an array.");
        }
    }

    // This function takes a JsonDocument object and a file path as input parameters.
    // It performs several operations to write the JSON document to a file and calculate and output the delta score.
    public static async Task WriteJsonDocumentToFile(JsonDocument jsonDocument, string filePath)
    {
        try
        {
            Console.WriteLine("Writing JSON document to file...");

            // Variables 
            List<JsonDocument> jsonDocuments = new List<JsonDocument>();
            double currentScore;
            double newCurrentScore;
            double deltaScore;

            // Process the first JSON document and add it to the list
            jsonDocuments = AddJsonDocumentToList(jsonDocuments, jsonDocument);

            
            // Validate the current score property
            if (!TryValidateCurrentScoreProperty(jsonDocument.RootElement, out currentScore))
            {
                Console.WriteLine("Error: Could not find or validate the currentScore property in the JSON document.");
                return;
            }

            // Create the secure score JSON document
            var secureScoreJson = await CreateSecureScoreJson(configuration);
            if (secureScoreJson != null)
            {
                // Process the second JSON document and add it to the list
                jsonDocuments = AddJsonDocumentToList(jsonDocuments, secureScoreJson);
            }
            
            // Extract the new current score from the secure score JSON document
            if (!TryGetNewCurrentScore(secureScoreJson, out newCurrentScore))
            {
                Console.WriteLine("Error: Could not find or validate the currentScore property in the secure score JSON document.");
                return;
            }
            
            // Calculate the delta score
            deltaScore = newCurrentScore - currentScore;
            Console.WriteLine("Delta Score: " + deltaScore + " (New Score: " + newCurrentScore + ", Current Score: " + currentScore + ")");
            
            // Output delta score information
            OutputDeltaScoreInformation(deltaScore);

            // Write the JSON documents to file
            await WriteSecureScoreJsonToFile(jsonDocuments, OUTPUT_PATH);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing JSON document to the file: " + ex.Message);
        }
    }

    // This function takes a list of JsonDocument objects, jsonDocuments, and a single JsonDocument object,
    // jsonDocument, as input parameters. Its purpose is to add the jsonDocument to the list in a specific way.
    public static List<JsonDocument> AddJsonDocumentToList(List<JsonDocument> jsonDocuments, JsonDocument jsonDocument)
    {
        if (jsonDocument != null)
        {
            // Get the root element of the JSON document
            JsonElement rootElement = jsonDocument.RootElement;

            // Check if the root element is an array
            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                // Enumerate through the array elements
                foreach (JsonElement arrayElement in rootElement.EnumerateArray())
                {
                    // Create a new JSON document for each array element
                    JsonDocument arrayElementDocument = JsonDocument.Parse(arrayElement.GetRawText());

                    // Add the array element document to the list
                    jsonDocuments.Add(arrayElementDocument);
                }
            }
            else
            {
                // Add the JSON document to the list as is
                jsonDocuments.Add(jsonDocument);
            }

            Console.WriteLine("JSON document(s) successfully added to the list.");
        }

        return jsonDocuments;
    }

    // This function takes a JsonElement object, rootElement, and an out parameter currentScore of type double.
    // Its purpose is to validate the presence and format of the "currentScore" property within 
    // the JSON document represented by the rootElement.
    public static bool TryValidateCurrentScoreProperty(JsonElement rootElement, out double currentScore)
    {
        currentScore = 0;

        if (rootElement.ValueKind == JsonValueKind.Object)
        {
            if (rootElement.TryGetProperty("currentScore", out JsonElement currentScoreProperty))
            {
                if (currentScoreProperty.ValueKind == JsonValueKind.Number && currentScoreProperty.TryGetDouble(out currentScore))
                {
                    ValidateJsonObject(rootElement);
                    return true;
                }
            }
        }
        else if (rootElement.ValueKind == JsonValueKind.Array)
        {
            JsonElement? lastObjectElement = null;
            foreach (JsonElement element in rootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("currentScore", out JsonElement currentScoreProperty))
                    {
                        if (currentScoreProperty.ValueKind == JsonValueKind.Number && currentScoreProperty.TryGetDouble(out currentScore))
                        {
                            lastObjectElement = element;
                        }
                    }
                }
            }

            if (lastObjectElement.HasValue)
            {
                ValidateJsonObject(lastObjectElement.Value);
                return true;
            }
        }
        else
        {
            return false;
        }
        return false;
    }

    // This function takes a JsonDocument object, secureScoreJson, and an out parameter newCurrentScore of type double.
    // Its purpose is to extract and validate the "currentScore" property from a secure score JSON document.
    public static bool TryGetNewCurrentScore(JsonDocument secureScoreJson, out double newCurrentScore)
    {
        newCurrentScore = 0;

        if (secureScoreJson != null)
        {
            var currentScoreFromJson = secureScoreJson.RootElement.GetProperty("currentScore");

            if (currentScoreFromJson.ValueKind == JsonValueKind.Number && currentScoreFromJson.TryGetDouble(out newCurrentScore))
            {
                Console.WriteLine("New Current Score from JSON file: " + newCurrentScore);
                return true;
            }
        }

        return false;
    }

    // The OutputDeltaScoreInformation function takes a deltaScore of type double as input and provides
    // an output message based on the value of the deltaScore. Its purpose is to display information about the change in secure score.
    public static void OutputDeltaScoreInformation(double deltaScore)
    {
        const double ZERO = 0;

        if (deltaScore == ZERO)
        {
            Console.WriteLine("No change in secure score! " + deltaScore);
        }
        else if (deltaScore > ZERO)
        {
            Console.WriteLine("Your secure score increased!! " + deltaScore);
        }
        else
        {
            Console.WriteLine("Your secure score decreased " + deltaScore);
        }
    }
}