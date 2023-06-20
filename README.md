# Secure Score Functions
Secure Score Functions is a C# console application that retrieves and processes secure score data from Azure. It uses the Azure SDK and Azure Identity library to authenticate with Azure, fetch secure score data, calculate score percentages, and create JSON files containing the score information.

## Prerequisites

Before running the code, ensure that the following prerequisites are met:

- Azure SDK and Azure Identity libraries are installed.
- An Azure subscription and valid Azure credentials are available.
- The `appsettings.json` file is properly configured with the required settings such as `Subscription_ID`, `SecureScoreName`, `Output_FilePath`, and `Az_FilePath`.

This project uses the following capabilities
.NET Core SDK (version X.X or higher)
Azure subscription
Azure CLI (for Az login

## Usage
To build the application, use the following command:

```shell
dotnet build
```

to run the application, use the following command:


```shell
dotnet run
```

## Running the Code

To run the code:

1. Set up the `appsettings.json` file with the appropriate configuration settings.
2. Build and execute the code using a C# compiler or an integrated development environment (IDE) with C# support.
3. The code will perform the necessary Azure login, retrieve and process secure score data, and generate the secure score JSON file.
4. If the specified output file path already exists, the code will process the existing JSON file, calculate the delta score, and write the updated JSON documents to the file.
5. Review the console output for status messages, including the output file path and any errors or success messages.

## Limitations

- The code assumes that the Azure CLI (`az`) executable is installed and properly configured.
- The code requires valid Azure credentials with appropriate permissions to access secure score data.
- The code is specific to the Azure SDK and Azure services and may not be directly applicable to other cloud platforms or APIs.
- The code does not handle all possible error scenarios and may require additional error handling and validation for production.

## License

This code is licensed under the [MIT License](LICENSE).

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvement, please open an issue or submit a pull request.

## Resources

- [Azure SDK for .NET](https://azure.github.io/azure-sdk/)
- [Azure Security Center Documentation](https://docs.microsoft.com/azure/security-center/)