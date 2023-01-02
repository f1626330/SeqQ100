
using System;
using System.Collections.Generic;
using System.Threading;


namespace Sequlite.ALF.Common
{
    public static class EventRaiser
    {
        #region Privet_Sections
        //private static ISeqLog logger = null;

        private static int MaximumNumberOfThreads = 64;

        private static void InvokeEventHandler(Delegate handler, object[] args)
        {
            handler.DynamicInvoke(args);
        }

        private static Delegate Raise(Delegate evHandler, params object[] args)
        {
            Delegate hd = evHandler;
            if (hd == null)
            {
                return null;
            }

            Delegate[] targets = hd.GetInvocationList();
            foreach (Delegate target in targets)
            {
                try
                {
                    InvokeEventHandler(target, args);
                }
                catch (Exception ex)
                {
                    string errMessage;
                    hd = RemoveEventHandler(hd, target, out errMessage);
                    errMessage += "EventRaiser Exception: " + ex.ToString();
                }
            }
            return hd;
        }


        private static Delegate RaiseAsync(Delegate evHandler,  params object[] args)
        {
            string errMessages = string.Empty;
            Delegate hd = evHandler;
            if (hd == null)
            {
                return null;
            }

            Delegate[] handlerList = hd.GetInvocationList();
            int maxNumThreads = MaximumNumberOfThreads;
            for (int i = 0; i <= handlerList.Length / (maxNumThreads + 1); ++i)
            {
                List<WaitHandle> waitHandles = new List<WaitHandle>();
                List<EventThread> eventTHandlehreads = new List<EventThread>();
                Delegate[] targetGroup;
                if (handlerList.Length - maxNumThreads * i > maxNumThreads)
                    targetGroup = new Delegate[maxNumThreads];
                else
                    targetGroup = new Delegate[handlerList.Length - maxNumThreads * i];

                Array.Copy(handlerList, maxNumThreads * i, targetGroup, 0, targetGroup.Length);
                foreach (Delegate target in targetGroup)
                {
                    EventThread eventThread = new EventThread(target, args);
                    waitHandles.Add(eventThread.WaitHandle);
                    eventTHandlehreads.Add(eventThread);
                    try
                    {
                        ThreadPool.QueueUserWorkItem(eventThread.WorkerThreadMain);
                    }
                    catch (Exception exp)
                    {
                        errMessages += string.Format("Event thread for target {0} has exception : {1}", target.ToString(), exp.ToString());
                    }
                }

                WaitForAll(waitHandles.ToArray());

                
                foreach (EventThread eventThread in eventTHandlehreads)
                {
                    if (!eventThread.Succeeded)
                    {
                        string errMsg;
                        hd = RemoveEventHandler(hd, eventThread.Target, out errMsg);
                        errMessages += errMsg;
                    }
                }

                foreach (WaitHandle w in waitHandles)
                {
                    w.Close();
                }
            }
            if (!string.IsNullOrEmpty(errMessages))
            {
                LogInternalError(errMessages);
            }
            return hd;
        }

      
        private static void WaitForAll(WaitHandle[] waitHandles)
        {
            for (int i = 0; i < waitHandles.Length; i++)
            {
                waitHandles[i].WaitOne();
            }
        }

        private static Delegate RemoveEventHandler(Delegate eventHandler, Delegate target, out string errMessage)
        {
            string targetName = target.ToString();
            errMessage = "EventRaiser: Event handler list " + eventHandler.ToString() + " has removed the handler " + targetName + ". ";
            return Delegate.Remove(eventHandler, target);
        }

        private class EventThread
        {
            private readonly object[] args;

            public readonly Delegate Target;
            public bool Succeeded;
            public readonly ManualResetEvent WaitHandle;

            public EventThread(Delegate target, object[] args)
            {
                this.Target = target;
                this.args = args;
                this.WaitHandle = new ManualResetEvent(false);
            }

            public void WorkerThreadMain(object state)
            {
                ThreadMain();
            }

            public void ThreadMain()
            {
                Succeeded = true;
                try
                {
                    InvokeEventHandler(Target, args);
                }
                catch (Exception ex)
                {
                    Succeeded = false;
                    throw ex;
                    //if (logger != null)
                    //{
                    //    logger.LogError("EventRaiser Error: " + ex.Message);
                    //}
                }
                WaitHandle.Set();
            }
        }
        #endregion


        #region Public_Static_Methods
        
        public static Delegate RaiseEvent(Delegate handler, params object[] args)
        {
            return Raise(handler, args);
        }
        public static Delegate RaiseEventAsync(Delegate handler, params object[] args)
        {
            return RaiseAsync(handler, args);
        }


        public static Delegate RaiseEvent(Delegate handler, object sender, EventArgs e)
        {
            return Raise(handler, sender, e);
        }
        public static Delegate RaiseEventAsync(Delegate handler, object sender, EventArgs e)
        {
            return RaiseAsync(handler, sender, e);
        }


        public static void RaiseEvent<T>(ref EventHandler<T> handler, object sender, T e) where T : EventArgs
        {
            handler = (EventHandler<T>)Raise(handler, sender, e);
        }
        public static void RaiseEventAsync<T>(ref EventHandler<T> handler, object sender, T e) where T : EventArgs
        {
            handler = (EventHandler<T>)RaiseAsync(handler, sender, e);
        }
        #endregion

        private static void LogInternalError(string logMsg)
        {
            if (Console.Out != null)
            {
                Console.Out.WriteLine(logMsg);
            }
        }
    }
}
