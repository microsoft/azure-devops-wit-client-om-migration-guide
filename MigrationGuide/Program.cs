using System;

namespace MigrationGuide
{
    class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return 0;
            }

            string sampleType, collectionUri, project;

            try
            {
                CheckArguments(args, out sampleType, out collectionUri, out project);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                ShowUsage();
                return -1;
            }

            try
            {
                Console.WriteLine($"Executing basic work item functionality samples for: '{sampleType}' types...");
                Console.WriteLine("");

                // Run appropriate sample, based on user's choice
                IClientScenarios exampleClient = CreateExampleClient(sampleType, collectionUri, project);
                exampleClient.CreateWorkItem();
                exampleClient.GetWorkItem();
                exampleClient.GetWorkItems();
                exampleClient.UpdateExistingWorkItem();
                exampleClient.ValidateWorkItem();
                exampleClient.GetWorkItemCategories();
                exampleClient.GetWorkItemCategory();
                exampleClient.GetWorkItemTypes();
                exampleClient.GetWorkItemType();
                exampleClient.GetWorkItemTypeFields();
                exampleClient.GetWorkItemTypeField();
                exampleClient.LinkExistingWorkItem();
                exampleClient.AddAttachment();
                exampleClient.QueryByWiql();
                exampleClient.QueryById();
                exampleClient.AddComment();
                exampleClient.AddHyperLink();

                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to run the sample: " + ex.Message);
                return 1;
            }

            return 0;
        }
        private static IClientScenarios CreateExampleClient(string sampleType, string collectionUri, string project)
        {
            switch (sampleType)
            {
                case ("REST"):
                    return new RESTClientScenarios(collectionUri, project);
                case ("OM"):
                default:
                    return new WITClientOMScenarios(collectionUri, project);
            }
        }
        private static void ShowUsage()
        {
            Console.WriteLine("Runs the WIT Object Model to REST API conversion samples on a Team Services account or Team Foundation Server instance.");
            Console.WriteLine("");
            Console.WriteLine("These samples are to provide you examples of how to convert your existing Object Model code to use the new REST API in Work Item Tracking.");
            Console.WriteLine("");
            Console.WriteLine("!!WARNING!! Some samples are destructive. Always run on a test account or collection.");
            Console.WriteLine("");
            Console.WriteLine("Arguments:");
            Console.WriteLine("");
            Console.WriteLine("  /url:fabrikam.visualstudio.com /project:projectname /type:OM|REST");
            Console.WriteLine("");

            Console.ReadKey();
        }
        private static void CheckArguments(string[] args, out string sampleType, out string connectionUri, out string project)
        {
            connectionUri = null;
            project = null;
            sampleType = "OM"; // default to OM examples

            foreach (var arg in args)
            {
                if (arg[0] == '/' && arg.IndexOf(':') > 1)
                {
                    string key = arg.Substring(1, arg.IndexOf(':') - 1);
                    string value = arg.Substring(arg.IndexOf(':') + 1);

                    switch (key)
                    {
                        case "url":
                            connectionUri = value;
                            break;
                        case "project":
                            project = value;
                            break;
                        case "type":
                            sampleType = value.ToUpper();
                            break;
                        default:
                            throw new ArgumentException("Unknown argument", key);
                    }
                }
            }

            if (connectionUri == null || project == null)
            {
                throw new ArgumentException("Missing required arguments");
            }
        }
    }
}