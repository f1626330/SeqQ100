using System.Collections.Generic;

namespace Sequlite.ALF.RecipeLib
{
    public class StepsTree
    {
        #region tree structure definitions
        public StepsTree Parent { get; set; }
        public List<StepsTree> Children { get; set; }
        #endregion tree structure definitions

        #region constructor
        public StepsTree(StepsTree parent, RecipeStepBase step)
        {
            Parent = parent;
            Step = step;
            Children = new List<StepsTree>();

            if (parent != null)
            {
                parent.Children.Add(this);
            }
        }
        #endregion constructor

        #region node properties
        public RecipeStepBase Step { get; set; }
        #endregion node properties

        #region Public Functions
        public void AppendSubStep(RecipeStepBase subStep)
        {
            new StepsTree(this, subStep);
        }
        public override string ToString()
        {
            if (Step == null) { return base.ToString(); }
            return Step.ToString();
        }
        #endregion Public Functions
    }
}
