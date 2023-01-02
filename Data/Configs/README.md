<!-- PAGE TITLE -->
# Documentation for Sequlite ALF Configuration (Config) and Calibration (Calib) Files

<!-- UPDATES -->
## Revisions

Version 3 - (2022 04 13)

* Add GreenPDMinCount and RedPDMinCount to Calibration.
* Add OLAMinimumTotalThreadCount to Configuration
* Add BENCHMARK log flag.

Version 2 - (2021 12 02)

* Add ImageTransforms to Calibration

Version 1 (unknown)

* Untracked development

<!-- CONFIG DOCUMENTATION -->
# Config.json

The **Config.json file** contains most settings that do not change between instruments. The file Config_default.json is automatically copied once during the first software installation from the source code to the calibration directory `C:/ProgramData/SequliteInstruments/Calibration/Config.json` The application loads the settings from the Config file in the calibration directory.

* ## LoggerConfig

  Used to set the types of messages logged by SeqLog. Messages for OLA  use the same flags but are handled by a seperate logger.
  * FilterOutFlags (string) [default = "DEBUG, TEST"]
  * OLAFilterOutFlags (string) [default = "DEBUG"]
    * Available flags:
    NONE = 0,
    NORMAL = 1,
    DEBUG = 2,
    TEST = 4,
    STARTUP = 8,
    OLAERROR = 16,
    OLAWARNING = 32,
    BENCHMARK = 64

* ## IsSimulation (bool) [default = false]

  Set to TRUE to enable simulation mode code

* ## IsMachineV2 (bool) [default = true]

  Set to TRUE to enable ALF 2.x code

* ## ProcessorAffinity (string) [default = "0x3FF"]

    A hexadecimal number in string form that is converted to a bitmask. Sets reserved threads for the GUI.  This setting works with  *OLAMinimumTotalThreadCount* to distribute processor cores between GUI and OLA. **(threads reserved for GUI + OLAMinimumTotalThreadCount = Total threads available on the hardware)**.
  * Examples:
  * 0x00 (b0): leave affinity unchanged
  * 0x3F (b111111): reserve 6 threads for GUI
  * 0xFF (b11111111): reserve 8 threads for the GUI
  * 0x3FF (b1111111111): reserve 10 threads for the GUI

* ## MaxMemoryUsage_GB (double) [default = 20]

  Reserve memory for the GUI and OS. The remainder of system memeory will be made available to OLA **(OLA RAM = System RAM - MaxMemoryUsage_GB)**. (units == [GB])

* ## OLAMinimumTotalThreadCount (double) [default = 118]

  Special setting to increase the number of threads allocated to OLA on Windows 10 systems with >64 logical cores. This setting works with *ProcessorAffinity* to distribute processor cores between GUI and OLA.

* ## ImagingBaseDirSelection (string) [default = "D:\\"]

    Base directory where image and OLA data are saved. Options:
  * MYDOC =  windows my document location
  * PROGRAMDATA = windows programData location
  * A full use specified path such as "C:\\temp" 

* ## RecipeRunLogSubDir (string) [default = "\\Sequlite\\ALF\\Recipe\\Recipelogs"]

  The subdirectory where recipe run logging files are stored. This directory will be a subdirectory of *ImagingBaseDirSelection*

* ## AcquiredImageSubDir (string) [default = "\\Sequlite\\ALF\\Recipe\\Images"]

  The subdirectory where acquired images are saved. This directory will be a subdirectory of *ImagingBaseDirSelection* 
  * Example: RecipeRunLogSubDir + AcquiredImageSubDir + "ExperimentName\\SequenceReadName(Read1/Index1/..)\\Data\\"

* ## AcquiredImageBackupLocation (string) [default = "\\\\SequLiteNAS2\\Experiments"]

  Image backup location. Images stored in *AcquiredImageSubDir* will be backed up to AcquiredImageBackupLocation + "ExperimentName\\SequenceReadName\\Data"

* ## DataBackupOptions (string) [default = "ImageBackup"]

  Select what data to backup. Available settings: "NoBackup", "ImageBackup", "OLABackup", or "ImageBackup,OLABackup"

* ## UseSubFolderForDataBackup (bool) [default = true]

  Set to TRUE to add the subdirectory "Instrument" to the path specified in *AcquiredImageBackupLocation*

* ## AnalysisTaskLocation (string) [default = "\\\\SequLiteNAS2\\Tasks"]

  The path to create a task for downstream analysis of OLA results

* ## MaximumReadlength (int) [default = 75]

  Maximum read length. This is used to estimate disk space required to store the sequencing data.

* ## SaveSimulationImages (bool) [default = true]

  Set to TRUE to save acquired images when running in simulation mode

* ## OLADir (string) [default = "C:\\bin"]

  Location for OLA executables and configuration files such as run.json (originally named run.sh)

* ## OLASimulationImageBaseDir (string) [default =""]

  The directory to search for sample images when running OLA in simulation mode. Must be a full path such as "C:\\OLA\\Images" or an empty string. If a full path is given, all images must be under its subdir "SequenceReadName\\Data" If an empty string is set, the default imaging location will be determined by the settings *ImagingBaseDirSelection* and *AcquiredImageSubDir*. Example: ImagingBaseDirSelection + AcquiredImageSubDir + "\\ExperimentName\\SequenceReadName\\Data"

* ## OLAProcessSimulationImages (bool) [default = false]

  Set to TRUE to use the images stored in *OLASimulationImageBaseDir* for OLA
  
* ## UsingTPL (bool) [default = true]

  Use Task parallel library to run OLA

* ## UsingTPLForExtractIntensitiesByCell (bool) [default = true]

  Set to TRUE to use Task Parallel Library to run extractInt

* ### OLASingleExtractMultipleImagesByCell (bool) [default = true]

  ?

* ## OLASingleExtractMultipleImagesByCellWithJC (bool) [default = false]

  Set to TRUE to have ExtractInt join cycles by itself

* ## OLAUseJoinCycles (bool) [default = false]

  Set to TRUE to join cycles before each BaseCall execution

* ## OLAJoinCyclesAtRunEnd (bool) [default = true]

  Cycles will be joined after all cycles are processed. Not needed for BaseCall, but is useful for downstream analysis. Cannot be done if *OLABaseCallOnlyPFClusters* is true, because all bli-s must have the same number of clusters.

* ## OLAMinimumCyclesToCreateTemplates (int) [default = 4]

  Use this # of cycles making sure OLAMinimumCyclesToCallBases is at least this big. 0: the software will select everything automatically.

* ## OLAMinimumCyclesToCallBases(int) [default = 5]

  Minmum cycle OLA run Basecalling, default 5

* ## OLABaseCallEveryNthCycle (int) [default = 5]

  Call bases, starting with *OLAMinimumCyclesToCallBases* cycle, every *OLABaseCallEveryNthCycle* cycles or on the last current cycle - whichever is larger

* ## OLASlidingWindowMain (bool) [default = true]

  Set to TRUE to use sliding window for Read1 and Read2

* ## OLASlidingWindowIndex (bool) [default = true]

  Set to TRUE to use sliding window for Index1 and Index2

* ## OLASlidingWindowOverlapCycles (int) [default = 2]

  Start the calling window from the cycle equal to the last cycle minus the overlap

* ## OLAMinimumOutput (bool) [default = true]

  ?

* ## OLAUseDLL (bool) [default = false]

  Use DLL instead of external exe

* ## OLAIndexCLR (bool) [default = false]

  If true, the proc-int-clr.fastq file of Read1 will be indexed. If false, only proc-int-cpf.fastq file of Read1 will be indexed.

* ## OLAUpdateCUIOnEveryTileBaseCall (bool) [default = true]

  If false, CUI will be updated only when all tiles are processed for the current range of cycles

* ## OLACatchHardwareExceptionsInCpp (bool) [default = false]

  ?

* ## OLABackupOffline (bool) [default = true]

  ?

* ## OLABaseCallOnlyPFClusters (bool) [default = true]

  Only run Basecalling for cluster that pass filter

* ## OLASparseMappingOption (int) [default = 1]

  See *OLASparseMappingCycles*

* ## OLASparseMappingCycles (int) [default = 0]

  Sparse mapping options to be used with the Sliding Window, for all base calling ranges:
  * 0: Do not use sparse mapping, i.e. always load all cycles up to the current. Ignore *OLASparseMappingCycles*.
  * 1: Load the minimum number of cycles required by the -x BaseCall parameter. Ignore *OLASparseMappingCycles*.
  * 2: Load a constant number of cycles equal to the *OLASparseMappingCycles*.
  * 3: Load all cycles for as long as PF clusters are not settled, then keep loading a constant number of cycles equal to the maximum number of cycles loaded during PF cluster search. Ignore *OLASparseMappingCycles*.
  * 4: same as 3, but, after PF clusters are settled, load a constant number of cycles equal to the *OLASparseMappingCycles*.
  
  **NOTE:**
  1. The sparse mapping configuration is used for setting the 11th phasing parameter (-g) for BaseCall
  2. If *OLABaseCallOnlyPFClusters* is TRUE, *OLASparseMappingOption* must be 1.

* ## OLAFirstCycleToUseYngrams (int) [deault = -1]

  Y-ngrams can be used only if the parameter file contains a YG entry. If y-ngrams are used, they will always be used on the last range of cycles.  If OLAFirstCycleToUseYngrams is <= 0, y-ngrams will be used only on the last range of cycles. If OLAFirstCycleToUseYngrams is > 0, y-ngrams will be first used on the first range of cycles, which includes the specified cycle. It is better to set OLAFirstCycleToUseYngrams to at least 30.

* ## OLAUseYngramsWithStepCycles (int) [default= -1]

  OLAUseYngramsWithStepCycles=N means y-ngrams will be used on all ranges starting with the first range where y-ngrams are used, then going with step N cycles, and ending with the last range of cycles. If OLAUseYngramsWithStepCycles is <=0, y-ngrams will be used only on 2 ranges: OLAFirstCycleToUseYngrams and the last range. If OLAUseYngramsWithStepCycles > 0, make this a multiple of the sliding window step, i.e. OLABaseCallEveryNthCycle.

* ## OLAOutOfRangeBaseCallAllowed (bool) [default = true]

* ## OLAUseScoresFromPreviousRangeWhenDefiningPFClusters (bool) [default = true]

* ## OLASmoothBCQC (bool) [default = true]

* ## OLASmoothBCQCIncludeOutOfRange (bool) [default = true]

* ## OLAParams_Main_nonHG (string) [default = "p_b165x2_no_r2_D12g8_noQM"]

* ## OLAParams_Main_HG (string) [default = "p_b165x2_YG_r2_D8g0_noQM"]

* ## OLAParams_Index (string) [default = "p_b165x2_idx_r2_D16"]
 
* ## BinFactors

  An array of camera binning settings used in the EUI. Available options: 1x1, 2x2, 3x3, 4x4, 6x6, 8x8

* ## Gains

  An array of camera gain options used in the EUI. Available options: 1x, 2x, 3x, 5x, 10x, 15x, 20x, 25x

* ## CameraDefaultSettings

  The default settings selected for the camera when the EUI starts.
  * BinFactor (int): index (position) of binning setting. [default = 1 (1x1 binning)]
  * Gain (int): index (position) of gain setting. [default = 1 (1x gain)]
  * RoiLeft (int): ROI start point from left side of image used for image cropping. [default = 0, units == pixels]
  * RoiTop (int): ROI start point from left side of image used for image cropping. [default = 0, units == pixels]
  * RoiWidth (int): Widthg of ROI used for image cropping. Set to 0 to disable cropping. [default = 0, units == pixels]
  * RoiHeight (int): Height of ROI used for image cropping. Set to 0 to disable cropping. [default = 0, units == pixels]
  * ReadoutSpeed (string): Used for V1 camera only [default = "Fast"]
  * ExtraExposure (double): Used for V1 camera only [default = 0.1, units == ?]

* ## Motion settings

  * Parameters and its range for stages
    * Includes Y stage, Z stage, Cartridge sipper motor, Filter wheel(V1 only), X stage(V2 only) and FC door(selected V2)
    * Contains Speed Range, Accelaration range, motion range
  * Default moition settings and parameters for each stages
    * Settings and parameters including speed, acceleration, absolute position, relative position
    * Stages includes filter wheel, Ystage, Zstage, cartridge sipper, X stage and FC door
  * Default motion settings and parameters used for home movement
    * Settings and parameters including speed, acceleration, absolute position, relative position
    * Stages includes filter wheel, Ystage, Zstage, cartridge sipper, X stage and FC door
  * Motion factor to convert physical unit to metric for stage controller, may different for some instrument
    * Contains Y stage, Z stage, Cartridge sipper motor, Filter wheel(V1 only), X stage(V2 only) and FC door(selected V2)
  * MotionEncoderFactors used in motion encoder

* ## Y-Stage regions

  * Only for V1 insturment
  * Contains position name, Y stage position and Z stage position

* ## FilterPositionSettings

 name and corresponding filter wheel contorller position

* ## LEDSettings

  Limit of power and on time for each LED
  * Contains Type, Intensity Range, MaxOnTime
    * Type contains Green, Red, White LED
    * Intensity Range contains lower and higher limit

* ## FluidicsSettings

* Fluidics system default settings
  * Pump Setting
    * Contains 8 parameters
      * Pump position to volumn factor, aspirate/dispense rate limit(high and low), pump volumn limit(high and low), and delay time after pull movement

* ## ChemiSetting

  * Chemistry FC heating plate setting limit
  * Contains 4 parameter
  * Temperature high and low limit, Ramp limit low and high

* ## DefaultPumping

* Default Pump setting
* Pump aspirate and dispense default rate, volumn and buffer1/2/3 selector valve position

* ## DefaultPumpPath

* Default valve position combination for specific fluidics path (V2 only)
        * Contains path: Tes1, Test2, Test3, Test4, BypassPrime, ByPass, TestByPass1, TestByPass2, FCLane1, FCLane2, FCLane3, FCLane4, FCL1L2, FCL2L3
    * Default chemsitry heating plate parameter
        * Contains temperature and ramp rate
* FC heating plate default controller PID
    * Contains  PID and HeatGain and CoolGain

* ## SerialCommDeviceSettings

* Serail com port position and baudRate for each hardware component
    * Contain hardware and corresponding portname and baudrate
        * Includes Z stage, Motion Controller, FC Heating plate Controller, Mainboard Controller, 
          Chiller, LED Controller, Fluidics Controller, BarCode Reader, RFID Reader, Valve2, Valve3, SelectorValve, Pump

* RecipeBuildConfig (sting): Default reicpe template name (all saved in ProgramData\Sequlite Instruments\\Recipes\\)
    * UsingOneRef (bool): whether auto mapping region, default true.
  
<!-- CALIB DOCUMENTATION -->
# Calib.json

* The Calib.json file contains calibration settings that change for each instrument. It is automatically copied once during the first software installation from the source code to the calibration directory <C:/ProgramData/SequliteInstruments/Calibration/Calib.json>
* **NOTE:** Changes to the calibration file will only take effect when they are made to the calibration file in the calibration directory.
* The software loads the settings from the Calib.json file in the in the calibration directory each time the software is launched.
* This file is usually modified during instrument qualification.

# JSON objects stored in Calib.json

* ## Version

* Date (string): the date the calibration file was modified
* ID (int): The version of the calibration file
* Product (string): The model of instrument the calibration file is for (V1 or V2)

* ## InstrumentInfo

  * Name of the instrument (string) that the calibration file was written for
  * FOV (double) Field of view (units = [mm]) 1.7 for 10x lens, 0.85 for 20x lens.

* ## AutoFocusingSettings

* ROI, ROI2: ROI used by camera to take the fiducial image for autofocusing
  * Contains four int to define the region of interest
* LEDType (string): default LED type used for autofocus, default - White,
* LEDIntensity (int): Intensity of LED used for autofocus
* ExposureTime (double): Autofocus Image exposure time unit seconds
* ZstageSpeed (int): Z stage movement speed for Autofocus
* ZstageAccel (int): Z stage movement accel for Autofocus
* ZRange (int): Z stage  movement range for searching focus
* FilterIndex (int): Filter wheel postion selection for Autofocus (V1 only)
* BottomOffset (double): Offset between fiducial and fluorescence focus point for bottom surface
* TopOffset (double): Offset between fiducial and fluorescence focus point for top surface
* ChannelOffset (double): Offset between green and red image channel
* OffsetLEDType (string): Default LED type used to take fluorescence image
* OffsetLEDIntensity (int): Default LED intensity used to take fluorescence image
* OffsetExposureTime (double): Default LED exposure time used to take fluorescence image 
* OffsetFilterIndex (int): Default filter wheel position used to take fluorescence image (V1 only)
* RotationAngle (double): Default rotation angle for fiducial image that make image more hortizontal 
* Reference0 (double): Estimated Z focus position (various depends on FC/instrument)
* TopStdLmtH (int): Estimated higher limit of sharpness for top surface, various depends on different FC manufacturer
* TopStdLmtL (int): Estimated lower limit of sharpness for top surface, various depends on different FC manufacturer
* BottomStdLmtL (int): Estimated lower limit of sharpness for bottom surface, various depends on different FC manufacturer
* FCChannelHeight (int): FC channel height, various depends on different FC manufacturer
* TopGlassThickness (int): FC top layer glass thickness, various depends on different FC manufacturer
* FiducialVersion (double): Sharpness calculation method version used in autofocus

* ## CameraCalibSettings

* GreenPDMinCount (double) [defaut = 1000]
  * The minimum value (unit = [counts] to read from the photodiode when testing the green LED
* RedPDMinCount (double) [default = 1000]
  * The minimum value (unit = [counts] to read from the photodiode when testing the red LED

* ## FluidicsCalibSettings

  * FCTestPressureCalib (int): Measured calibration test lane pressure 
  * ByPassPressureCalib (int): Measured calibration by pass lane pressure
  * PressureTole(int): Error tolerance for pressure test
  * FlowRateTole (double): Error tolerance for flow rate test
  * FlowRateStdTole (double): Flow rate standard deviation threshold 
  * WashCartPos (int): Measured postion of sipper with wash cartridge
  * ReagentCartPos (int): Measured position of sipper with reagent cartridge

* ## StageRegions

  * Stores information about FC region mapping related to X/Y stage movement position
  * Lane/Row/Column count(int)
  * Distance between each region in X and Y direction (double)
  * X/Y position of first region in each lane (double)

* ## ImageTransforms

  * Stores information related to image transformations used to correct for magnification and scaling differences between channels
  * **Warning:** The width of the final transformed image must be an even value. If the WidthAdjust and XOffset combine to produce an odd-width image, SaveNET will be unable to save the files. Fix this by incrementing or decrementing WidthAdjust or XOffset until an even image width is obtained.
  * Contains four objects, one for each imaging channel (G1, G2, R3, R4)
  * Each channel object contains four values
    * WidthAdjust (int) [default = 0]: The number of pixels to scale the image in the x-direction. Negative values shrink the image, positive values enlarge the image. Units = [px]
    * HeightAdjust (int) [default = 0]: The number of pixels to scale the image in the y-direction. Negative values shrink the image, positive values enlarge the image. Units = [px]
    * XOffset (int) [default = 0]: The number of pixels to translate the image in the x-direction after scaling. Negative values move the image to the up. Units = [px]
    * YOffset (int) [default = 0]: The number of pixels to translate the image in the y-direction after scaling. Negative values move the image to the left. Units = [px]

<!-- LICENSE -->
## License
Copyright (c) 2022 Sequlite. All rights reserved.