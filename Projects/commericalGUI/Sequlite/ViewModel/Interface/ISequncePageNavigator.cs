using Sequlite.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public enum SequencePageTypeEnum
    {
        SequenceWizard,
        User,
        Load,
        RunSetup,
        Check,
        Sequence,
        PostRun,
        Summary
    }

    public interface ISequncePageNavigator : IPageNavigator
    {
        ModelBase GetPageModel(SequencePageTypeEnum pageType);
        void AddPageModel(SequencePageTypeEnum pageType, ModelBase medel);
    }
}
