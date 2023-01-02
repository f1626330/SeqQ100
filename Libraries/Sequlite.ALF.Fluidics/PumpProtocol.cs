using System.Collections.Generic;

namespace Sequlite.ALF.Fluidics
{
    
    internal class PumpProtocol
    {
        #region Protocol Enums
        public enum CommandsEnum : byte
        {
            // Pump Config Commands
            MicroStepping,       // 'N'
            SaveConfig,          // 'U'
            SetBacklashInc,      // 'K'

            // Initialization Commands
            InitPlungerValveCW,  // 'Z'
            InitPlungerValveCCW, // 'Y'
            InitPlungerOnly,     // 'W'
            InitValveOnly,       // 'w'
            InitSimulation,      // 'z'
            SetZeroGap,          // 'k'

            // Valve Commands
            ValveToInput,        // 'I'
            ValveToOutput,       // 'O'
            ValveToByPass,       // 'B'
            ValveToExtra,        // 'E'

            // Plunger movement Commands/Status Bit Reports
            AbsPosMove,          // 'A'
            AbsPosIdle,        // 'a'
            RelPickupMove,       // 'P'
            RelPickupIdle,     // 'p'
            RelDispnsMove,       // 'D'
            RelDispnsIdle,     // 'd'

            // Set Commands
            SetSlope,            // 'L'
            SetStartSpeed,       // 'v'
            SetTopSpeed,         // 'V'
            SetSpeed,            // 'S'
            SetCutoffSpeed,      // 'c'
            SetCutoffInc,        // 'C'

            // Control Commands
            ExecuteCmd,          // 'R'
            ExecuteLastCmd,      // 'X'
            RepeatCmdSq,         // 'G'
            MarkRepeatStart,     // 'g'
            DelayExecution,      // 'M'
            HaltExecution,       // 'H'
            TerminateCmd,        // 'T'
            AuxiliaryOutput,     // 'J'

            // Report Commands
            ReportAbsPos,        // '?'
            ReportStartSpd,      // '?1'
            ReportTopSpd,        // '?2'
            ReportCutoffSpd,     // '?3'
            ReportActPos,        // '?4'
            ReportValvePos,      // '?6'

        }

        #endregion Protocol Enums

        #region Public Properties
        public static Dictionary<CommandsEnum, string> CommandsDic { get; }
        #endregion Public Properties

        #region Constructor
        static PumpProtocol()
        {
            CommandsDic = new Dictionary<CommandsEnum, string>();
            CommandsDic.Add(CommandsEnum.AbsPosMove, "A");
            CommandsDic.Add(CommandsEnum.AbsPosIdle, "a");
            CommandsDic.Add(CommandsEnum.ExecuteCmd, "R");
            CommandsDic.Add(CommandsEnum.InitPlungerOnly, "W");
            CommandsDic.Add(CommandsEnum.InitPlungerValveCCW, "Y");
            CommandsDic.Add(CommandsEnum.InitPlungerValveCW, "Z");
            CommandsDic.Add(CommandsEnum.InitSimulation, "z");
            CommandsDic.Add(CommandsEnum.InitValveOnly, "w");
            CommandsDic.Add(CommandsEnum.MicroStepping, "N");
            CommandsDic.Add(CommandsEnum.RelDispnsMove, "D");
            CommandsDic.Add(CommandsEnum.RelDispnsIdle, "d");
            CommandsDic.Add(CommandsEnum.RelPickupMove, "P");
            CommandsDic.Add(CommandsEnum.RelPickupIdle, "p");
            CommandsDic.Add(CommandsEnum.SaveConfig, "U");
            CommandsDic.Add(CommandsEnum.SetBacklashInc, "K");
            CommandsDic.Add(CommandsEnum.SetCutoffInc, "C");
            CommandsDic.Add(CommandsEnum.SetCutoffSpeed, "c");
            CommandsDic.Add(CommandsEnum.SetTopSpeed, "V");
            CommandsDic.Add(CommandsEnum.SetSlope, "L");
            CommandsDic.Add(CommandsEnum.SetSpeed, "S");
            CommandsDic.Add(CommandsEnum.SetStartSpeed, "v");
            CommandsDic.Add(CommandsEnum.SetZeroGap, "k");
            CommandsDic.Add(CommandsEnum.ValveToByPass, "B");
            CommandsDic.Add(CommandsEnum.ValveToExtra, "E");
            CommandsDic.Add(CommandsEnum.ValveToInput, "I");
            CommandsDic.Add(CommandsEnum.ValveToOutput, "O");
            CommandsDic.Add(CommandsEnum.HaltExecution, "H");
            CommandsDic.Add(CommandsEnum.TerminateCmd, "T");
            CommandsDic.Add(CommandsEnum.RepeatCmdSq, "G");
            CommandsDic.Add(CommandsEnum.MarkRepeatStart, "g");
            CommandsDic.Add(CommandsEnum.DelayExecution, "M");
            CommandsDic.Add(CommandsEnum.ReportValvePos, "?6");
            CommandsDic.Add(CommandsEnum.ReportAbsPos, "?");
            CommandsDic.Add(CommandsEnum.ReportActPos, "?4");
        }
        #endregion Constructor
    }
}
