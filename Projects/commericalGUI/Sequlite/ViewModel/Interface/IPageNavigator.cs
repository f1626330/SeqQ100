using Sequlite.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.UI.ViewModel
{
    public interface IPageNavigator
    {
        void MoveToPreviousPage();
        void MoveToNextPage();
        void CancelPage(bool confirmed = true);

        bool CanMoveToPreviousPage { get; set; }
        bool CanMoveToNextPage { get; set; }
        bool IsSimulation { get; set; }

        ModelBase GetPageModel(string pageType);
        void AddPageModel(string pageType, ModelBase medel);
    }

   
}
