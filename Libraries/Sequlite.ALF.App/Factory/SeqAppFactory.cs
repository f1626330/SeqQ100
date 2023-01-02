using System;
using System.Collections.Generic;
using System.Text;

namespace Sequlite.ALF.App
{
    public static class SeqAppFactory
    {
        private static Object LockInstanceCreation = new int[0];
        private static ISeqApp _SeqApp = null;
        public static ISeqApp GetSeqApp()
        {
            if (_SeqApp == null)
            {
                lock (LockInstanceCreation)
                {
                    if (_SeqApp == null)
                    {
                        _SeqApp = new SeqApp();
                    }
                }
            }
            return _SeqApp;
        }
    }
}
