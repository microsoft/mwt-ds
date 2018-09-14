# Dashboard Readme

## Usage
1. Create gzip file with dsjson events. For example, using:  
`mwt-ds\DataScience>python LogDownloader.py -a <appId> -l <path/to/folder> --create_gzip_mode 2`
2. (_Optional_) Create additional offline policies prediction files based on gzip-file created in Step 1 and place them in the same folder of the gzip-file (see _Additional Offline Policies_ below for more info).
3. Create file with aggregates from gzip-file:  
`mwt-ds\DataScience>python dashboard_utils.py -l <path-to-gzip-file> -o <path-to-output-file>`
4. Copy `mwt-ds\DataScience\dashboard.html` in some web accessible folder. For example, an Azure Storage folder with public access.
5. Go to: `https://your-url/dashboard.html?file=<filename>&show_ci=true&plot2=true` to see the dashboard with Online performance, Baseline1 estimated performance (this policy always selects first action), BaselineRand estimated performance (this policy chooses one action with uniform probability), and any additional offline policies added in Step 2.

### Additional Offline Policies
The dashboard can display additional offline policies (Step 2 above). In general, a policy produces a decision for each event (i.e., for each line in the gzip file). The prediction file collects these decision and must follow these conventions:
  - __Prediction file path__: It should be named `<path-to-gzip-file>.someName.pred` (so if you gzip file path is `c:\work\data.gz`, the prediction file can be `c:\work\data.gz.vwExp1.pred` - each file will be an item in the dashboard legend).
  - __Prediction file format__: One line for every line in the `<path-to-gzip-file>` (ignoring empty lines). Each line can either have one integer (the action selected by the offline policy) or a pdf over the actions (comma separated pairs of action:prob e.g., `3:0.1,1:0.5,4:0.2,2:0.1,0:0.1`). In both cases, actions are 0-base indexed - the first action is 0.

The prediction file can be generated using your favorite algo and following the above filename and format conventions. There are two other ways to generate it:
- Running `mwt-ds\DataScience>python Experimentation.py -f <path-to-gzip-file> --generate_pred`. This automatically create prediction files of policies found by Experimentation.py with correct file format and file location.
- Using Vowpal Wabbit command line (using flag `-p <file-path>` with `--cb_adf` or `--cb_explore_adf`). In this case <file-path> must follow the filename convention above.
