using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sequlite.ALF.RecipeLib;

namespace Sequlite.ALF.RecipeTool.ViewModel
{
    internal class CommentStepViewModel : StepsTreeViewModel
    {
        string _Comments = string.Empty;

        #region Constructor
        public CommentStepViewModel()
        {

        }
        public CommentStepViewModel(StepsTree content, StepsTreeViewModel parent) : base(content, parent)
        {
            CommentStep step = content.Step as CommentStep;
            if(step != null)
            {
                Comments = step.Comment;
            }
        }
        #endregion Constructor

        public string Comments
        {
            get { return _Comments; }
            set
            {
                if (_Comments != value)
                {
                    _Comments = value;
                    RaisePropertyChanged(nameof(Comments));
                }
            }
        }

        public override StepsTreeViewModel Clone()
        {
            CommentStepViewModel clonedVm = new CommentStepViewModel()
            {
                Comments = this.Comments
            };
            return clonedVm;
        }
    }
}
