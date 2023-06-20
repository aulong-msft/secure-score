using System.Diagnostics;

public static class AzHelpers
{
    // The AzLogin() function checks if the user is logged into Azure by executing the az account show command. 
    // It performs the Azure login if necessary and provides status messages for successful or failed login attempts.
    public static void AzLogin(string pathToAzExecutable)
    {
        if (pathToAzExecutable != null)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = pathToAzExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var accountShowProcess = new Process
            {
                StartInfo = processStartInfo,
            };

            accountShowProcess.StartInfo.Arguments = "account show";
            accountShowProcess.Start();
            string output = accountShowProcess.StandardOutput.ReadToEnd();
            string error = accountShowProcess.StandardError.ReadToEnd();
            accountShowProcess.WaitForExit();

            if (accountShowProcess.ExitCode == 0)
            {
                // The 'az account show' command succeeded, indicating that the user is already logged in
                Console.WriteLine("Azure login not required. Already logged in.");
            }
            else
            {
                // The 'az account show' command failed, indicating that the user is not logged in
                Console.WriteLine("Azure login required.");

                // Perform the Azure login
                var loginProcess = new Process
                {
                    StartInfo = processStartInfo,
                };
                loginProcess.StartInfo.Arguments = "login";
                loginProcess.Start();
                string loginOutput = loginProcess.StandardOutput.ReadToEnd();
                string loginError = loginProcess.StandardError.ReadToEnd();
                loginProcess.WaitForExit();

                if (loginProcess.ExitCode == 0)
                {
                    Console.WriteLine("Azure login successful.");
                }
                else
                {
                    Console.WriteLine("Azure login failed. Error: " + loginError);
                }
            }
        }
        else
        {
            Console.WriteLine("Error: The path to the az executable is not specified in the configuration.");
        }
    }
}