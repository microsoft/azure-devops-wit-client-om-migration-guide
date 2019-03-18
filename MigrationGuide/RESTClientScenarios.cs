using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MigrationGuide
{
    public class RESTClientScenarios : IClientScenarios
    {
        private WorkItemTrackingHttpClient WitClient { get; }
        private ProjectHttpClient ProjectClient { get; }
        private VssConnection Connection { get; }
        private TeamProjectReference TeamProject { get; }
        private HashSet<int> WorkItemsAdded { get; }

        public RESTClientScenarios(string collectionUri, string project)
        {
            Connection = new VssConnection(new Uri(collectionUri), new VssClientCredentials());
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            if (project != null)
            {
                TeamProject = ProjectClient.GetProject(project).Result;
            }

            WorkItemsAdded = new HashSet<int>();
        }

        public void AddAttachment()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using (FileStream fstream = File.Create(filePath))
            {
                using (StreamWriter swriter = new StreamWriter(fstream))
                {
                    swriter.Write("Sample attachment text");
                }
            }

            AttachmentReference attachment = WitClient.CreateAttachmentAsync(filePath).Result;
            Console.WriteLine("Attachment created");
            Console.WriteLine($"ID: {attachment.Id}");
            Console.WriteLine($"URL: '{attachment.Url}'");
            Console.WriteLine();
        }

        public void AddComment()
        {
            // Get a work item
            WorkItem wi = WitClient.GetWorkItemAsync(this.WorkItemsAdded.First()).Result;

            // Get the current last comment of the work item
            WorkItemComments comments = WitClient.GetCommentsAsync(wi.Id.Value).Result;
            var originalCommentCount = comments.Count;

            // Create a JSON patch document with an entry updating System.History
            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.History",
                    Value = "Added a comment"
                }
            };

            // Update the work item with the patch document
            var result = WitClient.UpdateWorkItemAsync(patchDocument, wi.Id.Value).Result;

            // Get the current last comment of the work item
            var updatedComments = WitClient.GetCommentsAsync(result.Id.Value).Result;
            var updatedCommentCount = updatedComments.Count;

            // Show that the current last comment is different than the original last comment
            Console.WriteLine($"There were {originalCommentCount} comments");
            Console.WriteLine($"There are now {updatedCommentCount} comments");
            Console.WriteLine();
        }

        public void AddHyperLink()
        {
            string hyperlinkToAdd = "https://www.microsoft.com";

            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new {
                        rel = "HyperLink",
                        url = hyperlinkToAdd,
                        attributes = new { comment = "Microsoft" }
                    }
                }
            };

            WorkItem wi = WitClient.GetWorkItemAsync(this.WorkItemsAdded.First()).Result;

            var relations = wi.Relations?.Where(r => r.Rel == "Hyperlink") ?? new List<WorkItemRelation>();
            var previousRelationsCount = relations.Count();

            var result = WitClient.UpdateWorkItemAsync(patchDocument, wi.Id.Value).Result;

            var newHyperlinks = result.Relations.Where(r => r.Rel == "Hyperlink");
            var newHyperlinksCount = newHyperlinks.Count();

            Console.WriteLine($"Updated Existing Work Item: '{wi.Id}'. Had {previousRelationsCount} hyperlinks, now has {newHyperlinksCount}");
            Console.WriteLine();
        }

        public void CreateWorkItem()
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = "Work Item Created Using REST Client"
                }
            };

            try
            {
                WorkItem wi = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Name, "User Story").Result;
                WorkItemsAdded.Add(wi.Id.Value);
                Console.WriteLine($"Created a work item with id: '{wi.Id}' and title: '{wi.Fields["System.Title"]}'");
            }
            catch (AggregateException ex)
            {
                Console.WriteLine("Error creating workitem: '{0}'", ex.InnerException.Message);
            }
            finally
            {
                Console.WriteLine();
            }
        }

        public void GetWorkItem()
        {
            WorkItem wi = WitClient.GetWorkItemAsync(WorkItemsAdded.First()).Result;
            Console.WriteLine($"Opened a work item with id: '{wi.Id}' and title: '{wi.Fields["System.Title"]}'");
            Console.WriteLine();
        }

        public void GetWorkItemCategories()
        {
            List<WorkItemTypeCategory> workItemCategories = WitClient.GetWorkItemTypeCategoriesAsync(TeamProject.Id).Result;
            var categoriesCount = workItemCategories.Count;
            Console.WriteLine($"Project: '{TeamProject.Name}' has the following {categoriesCount} categories:");
            foreach (WorkItemTypeCategory workItemTypeCategory in workItemCategories)
            {
                Console.WriteLine(workItemTypeCategory.Name);
            }
            Console.WriteLine();
        }

        public void GetWorkItemCategory()
        {
            var categoryName = "Requirement Category";
            WorkItemTypeCategory workItemCategory = WitClient.GetWorkItemTypeCategoryAsync(TeamProject.Id, categoryName).Result;
            Console.WriteLine($"Category with name: '{categoryName}' in Project: '{TeamProject.Name}' has reference name: '{workItemCategory.ReferenceName}' and {workItemCategory.WorkItemTypes.Count()} work item types");
            Console.WriteLine();
        }

        public void GetWorkItems()
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = "2nd Work Item Created Using REST Client"
                }
            };

            WorkItem wi = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Name, "Bug").Result;
            WorkItemsAdded.Add(wi.Id.Value);

            List<WorkItem> workItems = WitClient.GetWorkItemsAsync(WorkItemsAdded).Result;
            foreach (var workItem in workItems)
            {
                Console.WriteLine($"{workItem.Id}: '{workItem.Fields["System.Title"]}'");
            }
            Console.WriteLine();
        }

        public void GetWorkItemType()
        {
            var workItemTypeName = "Bug";
            var workItemType = WitClient.GetWorkItemTypeAsync(TeamProject.Id, workItemTypeName).Result;
            Console.WriteLine($"Obtained work item type '{workItemTypeName}', description: '{workItemType.Description}' using REST.");
            Console.WriteLine();
        }

        public void GetWorkItemTypeField()
        {
            var workItemTypeName = "Bug";
            var workItemTypeFieldName = "System.IterationPath";
            var workItemTypeField = WitClient.GetWorkItemTypeFieldWithReferencesAsync(TeamProject.Id, workItemTypeName, workItemTypeFieldName).Result;
            Console.WriteLine($"Obtained work item type field information for '{workItemTypeField.Name}' on project: '{TeamProject.Name}' using REST.");
            Console.WriteLine($"Always Required: '{workItemTypeField.AlwaysRequired}'");
            Console.WriteLine($"Help Text: '{workItemTypeField.HelpText}'");
            Console.WriteLine();
        }

        public void GetWorkItemTypeFields()
        {
            var workItemTypeName = "Bug";
            var workItemTypeFields = WitClient.GetWorkItemTypeFieldsWithReferencesAsync(TeamProject.Id, workItemTypeName).Result;
            Console.WriteLine($"Obtained all the work item type fields for '{workItemTypeName}' on project: '{TeamProject.Name}' using REST.");
            foreach (WorkItemTypeFieldWithReferences wiTypeField in workItemTypeFields)
            {
                Console.WriteLine($"Field '{wiTypeField.Name}'");
            }
            Console.WriteLine();
        }

        public void GetWorkItemTypes()
        {
            var workItemTypes = WitClient.GetWorkItemTypesAsync(TeamProject.Id).Result;
            var typesCount = workItemTypes.Count;
            Console.WriteLine($"Project: '{TeamProject.Name}' has the following {typesCount} types:");
            foreach (var wiType in workItemTypes)
            {
                Console.WriteLine(wiType.Name);
            }
            Console.WriteLine();
        }

        public void LinkExistingWorkItem()
        {
            // Get existing work item to link to new work item.
            WorkItem existingWI = WitClient.GetWorkItemAsync(WorkItemsAdded.First()).Result;

            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = "New work item to link to"
                },
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = existingWI.Url,
                        attributes = new
                        {
                            comment = "adding a link to an existing work item"
                        }
                    }
                }
            };

            // Create a new work item and link it to the existing work item.
            WorkItem newWI = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Id, "Bug").Result;
            Console.WriteLine($"Created a new work item Id:{newWI.Id}, Title:{newWI.Fields["System.Title"]}");
            foreach (var relation in newWI.Relations)
            {
                Console.WriteLine($"{relation.Rel} {relation.Title} {relation.Url}");
            }

            Console.WriteLine();
        }

        public void QueryByWiql()
        {
            Wiql queryByTypeWiql = new Wiql()
            {
                Query = $"Select [System.Id], [System.Title], [System.State] From WorkItems Where [System.WorkItemType] = 'Bug' and [System.TeamProject] = '{TeamProject.Name}'"
            };

            var typeQueryResults = WitClient.QueryByWiqlAsync(queryByTypeWiql).Result;

            Console.WriteLine($"The wiql query returned {typeQueryResults.WorkItems.Count()} results:");

            var workItemList = typeQueryResults.WorkItems.Select(wi => wi.Id);
            string[] fields = new string[] { "System.Id", "System.Title" };
            var workItems = WitClient.GetWorkItemsAsync(workItemList, fields).Result;

            foreach (WorkItem wi in workItems)
            {
                Console.WriteLine($"WorkItem Id: '{wi.Id}' Title: '{wi.Fields["System.Title"]}'");
            }
            Console.WriteLine();
        }

        public void QueryById()
        {
            // Get an existing query and associated ID for proof of concept.
            var queries = WitClient.GetQueriesAsync(TeamProject.Name, null, 1).Result;
            var myQueries = queries.Where(q => q.Name == "My Queries").First().Children;

            if (myQueries != null)
            {
                var firstQuery = myQueries.First();

                // Query by ID and process results
                var queryResults = WitClient.QueryByIdAsync(firstQuery.Id).Result;

                Console.WriteLine($"The query returned {queryResults.WorkItems.Count()} results:");
                if (queryResults.WorkItems.Count() > 0)
                {
                    var workItemList = queryResults.WorkItems.Select(wi => wi.Id);
                    string[] fields = new string[] { "System.Id", "System.Title" };
                    var workItems = WitClient.GetWorkItemsAsync(workItemList, fields).Result;

                    foreach (WorkItem wi in workItems)
                    {
                        Console.WriteLine($"WorkItem Id: '{wi.Id}' Title: '{wi.Fields["System.Title"]}'");
                    }
                }
                else
                {
                    Console.WriteLine($"Query with id:'{firstQuery.Id}' did not return any results.");
                }

                Console.WriteLine();
            }
            else
            {
                throw new Exception("My Queries haven't been populated yet. Open up the Queries page in the browser to populate these, and then run the sample again.");
            }
        }

        public void UpdateExistingWorkItem()
        {
            var wi = WitClient.GetWorkItemAsync(WorkItemsAdded.First()).Result;
            var originalTitle = wi.Fields["System.Title"];
            JsonPatchDocument patchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Title",
                    Value = "Changed Work Item Title Using REST Client"
                }
            };

            var result = WitClient.UpdateWorkItemAsync(patchDocument, wi.Id.Value).Result;

            Console.WriteLine($"Workitem: '{wi.Id}' title updated from: '{originalTitle}' to: '{result.Fields["System.Title"]}'");
            Console.WriteLine();
        }

        public void ValidateWorkItem()
        {
            try
            {
                // Create new work item
                var createPatchDocument = new JsonPatchDocument
                {
                    new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = "/fields/System.History",
                        Value = "Modify system history"
                    }
                };
                // Set validateOnly param to true and attempt to create a work item with an empty patch document.
                var validateOnCreateWI = WitClient.CreateWorkItemAsync(createPatchDocument, TeamProject.Name, "Bug", true).Result;
            }
            catch (AggregateException ex)
            {
                Console.WriteLine(ex.InnerException.Message);
                Console.WriteLine();
            }

            // Update existing work item
            try
            {
                var wi = WitClient.GetWorkItemAsync(WorkItemsAdded.First()).Result;
                var updatePatchDocument = new JsonPatchDocument
                {
                    new JsonPatchOperation()
                    {
                        Operation = Operation.Add,
                        Path = "/fields/System.AreaPath",
                        Value = "Invalid area path"
                    }
                };

                // Set validateOnly param to true and attempt to update a work item with an invalid field entry.
                var validateOnUpdateWI = WitClient.UpdateWorkItemAsync(updatePatchDocument, wi.Id.Value, true).Result;
            }
            catch (AggregateException ex)
            {
                Console.WriteLine(ex.InnerException.Message);
                Console.WriteLine();
            }

            Console.WriteLine();
        }
    }
}
