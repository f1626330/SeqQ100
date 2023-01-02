using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class RecipeBuildSettings
    {
        public string ClusterGenRecipePath => GetFullFileName(ClusterGenRecipe);

        public string HybRecipePath  => GetFullFileName(HybRecipe);
        public string IncRecipePath => GetFullFileName(IncRecipe);
        public string CLRecipePath => GetFullFileName(CLRecipe);
        public string PairedTRecipePath => GetFullFileName(PairedTRecipe);
        public string PostWashRecipePath => GetFullFileName(PostWashRecipe);
        public string SeqWashRecipePath => GetFullFileName(SeqWashRecipe);
        public string ManualPostWashRecipePath => GetFullFileName(ManualPostWashRecipe);
        public string MaintenanceWashRecipePath => GetFullFileName(MaintenanceWashRecipe);
        public string OriginalImageRecipePath => GetFullFileName(OriginalImageRecipe);
        public string PrimingRecipeRecipePath => GetFullFileName(PrimingRecipeRecipe);
        public string IndexHybRecipePath => GetFullFileName(IndHybRecipe);
        public string StpRecipePath => GetFullFileName(StpRecipe);
        //from json

        public string ClusterGenRecipe { get; set; }
        public string HybRecipe { get; set; }
        public string IncRecipe { get; set; }
        public string CLRecipe { get; set; }
        public string PairedTRecipe { get; set; }
        public string PostWashRecipe { get; set; }
        public string OriginalImageRecipe { get; set; }
        public string SeqWashRecipe { get; set; }
        public string MaintenanceWashRecipe { get; set; }
        public string ManualPostWashRecipe { get; set; }
        public string PrimingRecipeRecipe { get; set; }
        public string IndHybRecipe { get; set; }
        public string StpRecipe { get; set; }

        public bool UsingOneRef { get; set; }

        private string GetFullFileName(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                return System.IO.Path.Combine(RecipeBaseDir, fileName);
            }
            else
            {
                return string.Empty;
            }
        }

        public string RecipeBaseDir {  get; set; }

    }
}
