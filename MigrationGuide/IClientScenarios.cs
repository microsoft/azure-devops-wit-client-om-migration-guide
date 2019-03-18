using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MigrationGuide
{
    public interface IClientScenarios
    {
        void CreateWorkItem();
        void GetWorkItem();
        void GetWorkItems();
        void UpdateExistingWorkItem();
        void ValidateWorkItem();
        void LinkExistingWorkItem();
        void AddComment();
        void AddHyperLink();
        void AddAttachment();
        void QueryByWiql();
        void QueryById();
        void GetWorkItemCategories();
        void GetWorkItemCategory();
        void GetWorkItemTypes();
        void GetWorkItemType();
        void GetWorkItemTypeFields();
        void GetWorkItemTypeField();
    }
}
