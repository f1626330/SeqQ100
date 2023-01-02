# :shipit: Updating Software on an Instrument

This document contains instructions for safely deploying software updates to ALF instruments.

## :exclamation: Special note when updating to Version 3.5.6 and later

**Warning:** The behavior of the files Config.json and Calib.json has changed starting with software version 3.5.6.

- Prior to v3.5.6:
  - Config.json was loaded from the build directory and copied from the source code to the build directory every time the application was built. Pulling changes to the source code could overwrite local changes made to this file.
  - Calib.json was loaded from the application data directory `C:\ProgramData\Sequlite Instruments\Calibration`. This file was only copied from the source code if no file existed yet in the appliation data directory.

- Beginning with v3.5.6:
  - Both Config.json and Calib.json are loaded from the application data directory. `C:\ProgramData\Sequlite Instruments\Calibration` If these files do not exist yet, they are copied from the files in the source code named Config_default.json and Calib_default.json, respectively.
  - The version of both Config.json and Calib.json is checked on application launch to ensure that up-to-date files are used. If the version is out of date, the application will throw an exception and will not launch.

1. **Before updating an instrument to v3.5.6 or later, backup the files Config.json and Calib.json**.
    - Copy the backed up files outside of the git repository
2. After pulling changes, merge each file with the updated version and keep the local changes. `git diff Config.json Config_default.json`
3. At the end of the merging process, the files `Config.json` and `Calib.json` located in `C:\Program Data\Sequlite Instruments\Calibration` should retain all the instrument-specific parameters as well as contain all of the parameters found in the current versions of the default files `Config_default.json` and `Calib_default.json`.
4. After the merge process is complete, the only files that need to remain in the repository are `Config_default.json` and `Calib_default.json`. If there are residual files (like Config.json or backups) stored in `Alf/Data/Configs`, they can be removed.
5. This merging procedure is only required when updating from a version before 3.5.6 to 3.5.6 or later. It is not required again after updating to 3.5.6.

## :microscope: Software Update Process

1. Save all files with local changes.
2. Inspect the files that have changed using the Git Changes panel inside Visual Studio and decide what if any edits need to be preserved. Edits can be *permanent* and propagate back to the main repository or *temporary* and stay only on a specific machine (for example, to disable a broken sensor). See a list of **Unique Modifications** (*temporary* edits to the source code) made on each of the ALF 2.x instruments and details on how to enable or disable them [here](https://docs.google.com/spreadsheets/d/1t-O-woN_ImEyOVCCndyaG1oNW39_1Jse0z7cDUwV81c/edit?usp=sharing).
3. For *permanent* edits:
    1. If the local repository is not already on an issue branch, create a new issue branch and carry all of the changed files to the new branch.
    2. Stage and commit *permanent* edits, but not *temporary* edits.
        - If well-tested, these edits can be pulled into the developer branch by pushing the issue branch and submitting a pull request.
    3. Only *temporary* edits should now remain in the repository.
4. For *temporary* edits:
    - Stash temporary edits in the local repository.
5. Fetch the latest updates from the origin.
6. Checkout the lastest updates
    - Either switch your local branch to the head of your remote branch or merge the updates into your local repository.
      - To merge, right click on the branch with the updates you want (usually *development*) and select *merge into*.
      - This will create a merge commit and overwrite local changes that have not been committed.
7. Restore the *temporary* local changes from the stash.
    - This will overwrite again with the *temporary* changes that were in the stash.
8. Verify that local changes have been correctly restored.
    - Check the diffs for each file and make sure the modifications were correctly applied before deleting the stash. `git diff file1 file2`
9. Delete the stash.
    - **Warning:** make sure the changes stored in the stash have been applied before deleting the stash.
  
  If the version number of the Calib or Config file has changed (parameters have been added or removed), complete the  additional steps outlined in the Calibration or Configuration File Update Process (below) to finish the update.

## :bookmark_tabs: Calibration or Configuration File Update Process

1. Refer to the documentation or inspect the new Calib and Config files to determine what parameters have been added or removed. `git diff Config.json Config_default.json`

2. Marge the local config or calib file with the updated file. If new parameters are available, ensure they are set to an appropriate value.
