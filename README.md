# Secure Score Functions

This code is a set of functions that interact with Azure Defender for Cloud's Secure Score API. It allows you to retrieve secure score data from Azure, process and manipulate JSON files, and perform various operations on the secure score data.

## Prerequisites

Before running the code, make sure you have completed the following steps:

1. Install the Azure SDK for .NET.
2. Fill out the `Subscription_ID` and perform an `az login` in the `appsettings.json` file.
3. Ensure that you have the necessary permissions and access to the Azure subscription.

## Usage

1. Clone the repository or download the code files.

2. Open the code in your preferred development environment.

3. Modify the `appsettings.json` file with your Azure subscription ID and other required information.

4. Run the code.

5. The application will prompt you to choose between processing a new secure score or building one over time.

   - If you choose "new", the code will retrieve the secure score data from Azure, create a new secure score JSON file, and save it to the specified output path.

   - If you choose "build", the code will prompt you to enter the path to an existing JSON file. It will then process the JSON file, extract the secure score data, and perform operations on it.

6. The output will be displayed in the console, including the secure score JSON file path, the generated secure score JSON, and any relevant information or changes in the secure score.

## File Structure

- `Program.cs`: Contains the main entry point and functions for retrieving, processing, and manipulating secure score data.
- `appsettings.json`: Configuration file with Azure subscription ID and other required settings.

## License

This code is licensed under the [MIT License](LICENSE).

## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvement, please open an issue or submit a pull request.

## Resources

- [Azure SDK for .NET](https://azure.github.io/azure-sdk/)
- [Azure Security Center Documentation](https://docs.microsoft.com/azure/security-center/)

