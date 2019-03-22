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
        private readonly string DefaultCategoryReferenceName = "Microsoft.RequirementCategory";
        private WorkItemTypeCategory DefaultWorkItemTypeCategory { get; }
        private WorkItemTypeReference DefaultWorkItemType { get; }

        public RESTClientScenarios(string collectionUri, string project)
        {
            Connection = new VssConnection(new Uri(collectionUri), new VssClientCredentials());
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            TeamProject = ProjectClient.GetProject(project).Result;
            WorkItemsAdded = new HashSet<int>();
            DefaultWorkItemTypeCategory = WitClient.GetWorkItemTypeCategoryAsync(TeamProject.Id, DefaultCategoryReferenceName).Result;
            DefaultWorkItemType = DefaultWorkItemTypeCategory.DefaultWorkItemType;
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
                },
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.AssignedTo",
                    Value = Connection.AuthorizedIdentity.DisplayName
                }
            };

            try
            {
                WorkItem wi = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Name, DefaultWorkItemType.Name).Result;
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

            WorkItem wi = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Name, DefaultWorkItemType.Name).Result;
            WorkItemsAdded.Add(wi.Id.Value);

            // GetWorkItemsAsync can only return 200 items at a time, so only take the first 200 of the work items list.
            // Larger lists will require batching calls to GetWorkItemsAsync until the list is processed.
            List<WorkItem> workItems = WitClient.GetWorkItemsAsync(WorkItemsAdded.Take(200)).Result;
            foreach (var workItem in workItems)
            {
                Console.WriteLine($"{workItem.Id}: '{workItem.Fields["System.Title"]}'");
            }
            Console.WriteLine();
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
                // Set validateOnly param to true and attempt to create a work item with an incomplete patch document (missing required title field).
                var validateOnCreateWI = WitClient.CreateWorkItemAsync(createPatchDocument, TeamProject.Name, DefaultWorkItemType.Name, true).Result;
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

        public void LinkExistingWorkItem()
        {
            // Get existing work item to link to new work item.
            WorkItem existingWI = WitClient.GetWorkItemAsync(WorkItemsAdded.First()).Result;

            // Create a patch document for a new work item.
            // Specify a relation to the existing work item.
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

            // Create a new work item and link it to the existing work item (using patchdocument)
            WorkItem newWI = WitClient.CreateWorkItemAsync(patchDocument, TeamProject.Id, DefaultWorkItemType.Name).Result;
            Console.WriteLine($"Created a new work item Id:{newWI.Id}, Title:{newWI.Fields["System.Title"]}");
            foreach (var relation in newWI.Relations)
            {
                Console.WriteLine($"{relation.Rel} {relation.Title} {relation.Url}");
            }

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

            var newHyperlinks = result.Relations?.Where(r => r.Rel == "Hyperlink");
            var newHyperlinksCount = newHyperlinks.Count();

            Console.WriteLine($"Updated Existing Work Item: '{wi.Id}'. Had {previousRelationsCount} hyperlinks, now has {newHyperlinksCount}");
            Console.WriteLine();
        }

        public void AddAttachment()
        {
            // Create a file to attach with sample text
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using (FileStream fstream = File.Create(filePath))
            {
                using (StreamWriter swriter = new StreamWriter(fstream))
                {
                    swriter.Write("Sample attachment text");
                }
            }

            // Upload attachment
            AttachmentReference attachment = WitClient.CreateAttachmentAsync(filePath).Result;
            Console.WriteLine("Attachment created");
            Console.WriteLine($"ID: {attachment.Id}");
            Console.WriteLine($"URL: '{attachment.Url}'");
            Console.WriteLine();

            // Get an existing work item and add the attachment to it
            WorkItem wi = WitClient.GetWorkItemAsync(this.WorkItemsAdded.First()).Result;
            JsonPatchDocument attachmentPatchDocument = new JsonPatchDocument
            {
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "AttachedFile",
                        url = attachment.Url,
                        attributes = new
                        {
                            comment = "Attached a file"
                        }
                    }
                }
            };

            var attachments = wi.Relations?.Where(r => r.Rel == "AttachedFile") ?? new List<WorkItemRelation>();
            var previousAttachmentsCount = attachments.Count();

            var result = WitClient.UpdateWorkItemAsync(attachmentPatchDocument, wi.Id.Value).Result;

            var newAttachments = result.Relations?.Where(r => r.Rel == "AttachedFile");
            var newAttachmentsCount = newAttachments.Count();

            Console.WriteLine($"Updated Existing Work Item: '{wi.Id}'. Had {previousAttachmentsCount} attachments, now has {newAttachmentsCount}");
            Console.WriteLine();
        }

        public void QueryByWiql()
        {
            Wiql wiql = new Wiql()
            {
                Query = $"Select [System.Id], [System.Title], [System.State] From WorkItems Where [System.WorkItemType] = '{DefaultWorkItemType.Name}' and [System.TeamProject] = '{TeamProject.Name}'"
            };

            var queryResults = WitClient.QueryByWiqlAsync(wiql).Result;

            Console.WriteLine($"The wiql query returned {queryResults.WorkItems.Count()} results:");

            var workItemList = queryResults.WorkItems.Select(wi => wi.Id);
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
            // Get the root query folders
            var queries = WitClient.GetQueriesAsync(TeamProject.Name, null, 1).Result;
            var myQueriesFolder = queries.Where(q => !q.IsPublic.Value).FirstOrDefault();

            if (myQueriesFolder != null && myQueriesFolder.IsFolder.GetValueOrDefault(false) && myQueriesFolder.HasChildren.GetValueOrDefault(false))
            {
                var firstQuery = myQueriesFolder.Children.First();

                // Query by ID and process results
                var queryResults = WitClient.QueryByIdAsync(firstQuery.Id).Result;
                Console.WriteLine($"Query with name: '{firstQuery.Name}' and id: '{firstQuery.Id}' returned {queryResults.WorkItems.Count()} results:");
                if (queryResults.WorkItems.Count() > 0)
                {
                    // GetWorkItemsAsync can only return 200 items at a time, so only take the first 200 of the work items list.
                    // Larger lists will require batching calls to GetWorkItemsAsync until the list is processed.
                    var workItemList = queryResults.WorkItems.Select(wi => wi.Id).Take(200);
                    string[] fields = new string[] { "System.Id", "System.Title" };
                    var workItems = WitClient.GetWorkItemsAsync(workItemList, fields).Result;

                    foreach (WorkItem wi in workItems)
                    {
                        Console.WriteLine($"WorkItem Id: '{wi.Id}' Title: '{wi.Fields["System.Title"]}'");
                    }
                }
                else
                {
                    Console.WriteLine($"Query with name: '{firstQuery.Name}' and id: '{firstQuery.Id}' did not return any results.");
                    Console.WriteLine($"Try assigning work items to yourself or following work items and run the sample again.");
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("My Queries haven't been populated yet. Open up the Queries page in the browser to populate these, and then run the sample again.");
            }
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
            WorkItemTypeCategory workItemCategory = WitClient.GetWorkItemTypeCategoryAsync(TeamProject.Id, DefaultCategoryReferenceName).Result;
            Console.WriteLine($"Category with reference name: '{DefaultCategoryReferenceName}' in Project: '{TeamProject.Name}' has name: '{workItemCategory.Name}' and {workItemCategory.WorkItemTypes.Count()} work item types");
            Console.WriteLine();
        }

        public void GetWorkItemTypes()
        {
            var types = WitClient.GetWorkItemTypesAsync(TeamProject.Id).Result;
            var typesCount = types.Count;
            Console.WriteLine($"Project: '{TeamProject.Name}' has the following {typesCount} types:");
            foreach (var type in types)
            {
                Console.WriteLine($"Name: '{type.Name}' - Description: '{type.Description}'");
            }
            Console.WriteLine();
        }

        public void GetWorkItemType()
        {
            var workItemTypeName = DefaultWorkItemType.Name;
            var workItemType = WitClient.GetWorkItemTypeAsync(TeamProject.Id, workItemTypeName).Result;
            Console.WriteLine($"Obtained work item type '{workItemTypeName}', description: '{workItemType.Description}' using REST.");
            Console.WriteLine();
        }

        public void GetWorkItemTypeFields()
        {
            var workItemTypeName = DefaultWorkItemType.Name;
            var workItemTypeFields = WitClient.GetWorkItemTypeFieldsWithReferencesAsync(TeamProject.Id, workItemTypeName).Result;
            Console.WriteLine($"Obtained all the work item type fields for '{workItemTypeName}' on project: '{TeamProject.Name}' using REST.");
            foreach (WorkItemTypeFieldWithReferences wiTypeField in workItemTypeFields)
            {
                Console.WriteLine($"Field '{wiTypeField.Name}'");
            }
            Console.WriteLine();
        }

        public void GetWorkItemTypeField()
        {
            var workItemTypeName = DefaultWorkItemType.Name;
            var workItemTypeFieldName = "System.Title";
            var workItemTypeField = WitClient.GetWorkItemTypeFieldWithReferencesAsync(TeamProject.Id, workItemTypeName, workItemTypeFieldName).Result;

            Console.WriteLine($"Obtained work item type field information for '{workItemTypeFieldName}' on project: '{TeamProject.Name}' using REST.");
            Console.WriteLine($"Name: '{workItemTypeField.Name}'");
            Console.WriteLine($"Reference Name: '{workItemTypeField.ReferenceName}'");
            Console.WriteLine($"Always Required: '{workItemTypeField.AlwaysRequired}'");
            Console.WriteLine($"Help Text: '{workItemTypeField.HelpText}'");
            Console.WriteLine();
        }
    }
}
