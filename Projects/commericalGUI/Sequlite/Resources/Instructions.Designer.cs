﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Sequlite.UI.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Instructions {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Instructions() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Sequlite.UI.Resources.Instructions", typeof(Instructions).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Check:&lt;/u&gt; System verifies everything is in order (hardware, user input, etc.).&lt;/h4&gt;
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;User interaction fields:
        ///                        &lt;ol&gt; 
        ///                          &lt;li&gt;Start Run:&lt;/li&gt;
        ///                        &lt;/ol&gt;
        ///                    &lt;/li&gt;
        ///              &lt;/ul&gt;  
        ///  
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;System Checks
        ///                      &lt;ol&gt; 
        ///                          &lt;li&gt;Doors Closed&lt;/li&gt;
        ///                          &lt;li&gt;Consumables Loaded [rest of string was truncated]&quot;;.
        /// </summary>
        public static string CheckPage_Instruction {
            get {
                return ResourceManager.GetString("CheckPage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Load:&lt;/u&gt; Instructs the user to load the flow cell, cartridge, waste, etc.&lt;/h4&gt;
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;User interaction fields:
        ///                      &lt;ol&gt; 
        ///                          &lt;li&gt;Next(Buttons) to move to the next component to be loaded&lt;/li&gt;
        ///                          &lt;li&gt;Grace Hopper&lt;/li&gt;
        ///                          &lt;li&gt;Load(Buttons)&lt;/li&gt;
        ///                      &lt;/ol&gt;
        ///                    &lt;/li&gt;
        ///              &lt;/ul&gt;  
        ///  
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;Displ [rest of string was truncated]&quot;;.
        /// </summary>
        public static string LoadPage_Instruction {
            get {
                return ResourceManager.GetString("LoadPage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Automatic Post-Run Wash:&lt;/u&gt; NaOCI&lt;/h4&gt;
        ///             
        ///            &lt;ol&gt; 
        ///                &lt;li&gt;~90 mins&lt;/li&gt;
        ///                &lt;li&gt;Sippers remain in the down position, replace next run&lt;/li&gt;
        ///                &lt;li&gt;Leave the used FC reagent/ buffer&lt;/li&gt;
        ///            &lt;/ol&gt;.
        /// </summary>
        public static string PostRunPage_Instruction {
            get {
                return ResourceManager.GetString("PostRunPage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Setup Run Parameters:&lt;/u&gt; Asks user to verify run configurations, depending on the kit.&lt;/h4&gt;
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;User interaction fields:
        ///                      &lt;ol&gt; 
        ///                          &lt;li&gt;Run selection&lt;/li&gt;
        ///                          &lt;li&gt;Edit(Button):&lt;/li&gt;
        ///                          &lt;li&gt;Save(Button):&lt;/li&gt;
        ///                          &lt;li&gt;Next: move to pre-run check&lt;/li&gt;
        ///                      &lt;/ol&gt;
        ///                    &lt;/li&gt;
        ///              &lt;/ul&gt;  
        ///  
        ///               [rest of string was truncated]&quot;;.
        /// </summary>
        public static string RunSetupPage_Instruction {
            get {
                return ResourceManager.GetString("RunSetupPage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Sequencing Run:&lt;/u&gt; SW selects corresponding recipe to run and display key information.&lt;/h4&gt;
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;User interaction fields:
        ///                        &lt;ol&gt; 
        ///                          &lt;li&gt;Start Stop:&lt;/li&gt;
        ///                          &lt;li&gt;Start Pause:&lt;/li&gt;
        ///                          &lt;li&gt;Start Start/Continue:&lt;/li&gt;
        ///                        &lt;/ol&gt;
        ///                    &lt;/li&gt;
        ///              &lt;/ul&gt;  
        ///  
        ///              &lt;ul&gt;
        ///                    &lt;li&gt;Display info
        ///            [rest of string was truncated]&quot;;.
        /// </summary>
        public static string SequencePage_Instruction {
            get {
                return ResourceManager.GetString("SequencePage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; Summary:&lt;/u&gt; to do&lt;/h4&gt;   
        ///                &lt;ol&gt; 
        ///                    &lt;li&gt;Run Summary.&lt;/li&gt;
        ///                    &lt;li&gt;Charts &amp; Graphs.&lt;/li&gt;
        ///                    &lt;li&gt;Sample Sheet.&lt;/li&gt;
        ///                &lt;/ol&gt;.
        /// </summary>
        public static string SummaryPage_Instruction {
            get {
                return ResourceManager.GetString("SummaryPage_Instruction", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;h4&gt;&lt;u&gt; User:&lt;/u&gt; User Profile&lt;/h4&gt;   
        ///                &lt;ol&gt; 
        ///                    &lt;li&gt;Name.&lt;/li&gt;
        ///                    &lt;li&gt;Email.&lt;/li&gt;
        ///                    &lt;li&gt;Recent Run History.&lt;/li&gt;
        ///                &lt;/ol&gt;.
        /// </summary>
        public static string UserPage_Instruction {
            get {
                return ResourceManager.GetString("UserPage_Instruction", resourceCulture);
            }
        }
    }
}
