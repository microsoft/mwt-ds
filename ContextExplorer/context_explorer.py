import datetime
import dateutil
import itertools
import json
import operator
import os
import warnings
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.dates as mdates 
from matplotlib.ticker import MultipleLocator

import multiprocessing
import numpy as np
import pandas as pd
from functools import reduce
from collections import defaultdict
from scipy import stats

class ContextExplorer():
    '''
    Provide context-specific analysis and reports for online Contextual Bandit AB Tests.
    '''
    
    def __init__(self, config_file, p_threshold=0.001, ci_std_mean=False):
        '''
        config_file[str]: path to config file
        p_threshold [float]: p-value threshold to determine if a t-test result is statistically significant
        ci_std_mean [bool]: whether to show the confidence interval as 1.96*std of the mean or the raw data
        today [date]: overwrite the system date by specifying a different end date for all experiment
        '''
        configs = json.load(open(config_file))
        config_exps = configs['exps']
        self.output_folder = configs['output_folder']
        self.top_n = configs.get('show_top_sensitive_contexts', 20)
        self.min_sample = configs.get('min_daily_sample', 200)
        self.name_cols()
        self.prep_path()
        self.config_exps = self.complete_config_dates(config_exps)
        self.config_exps = self.complete_config(config_exps)
        self.p_threshold = p_threshold
        self.ci_std_mean = ci_std_mean
        self.font_name = 'Arial'
        self.font_family = 'sans-serif'

    def name_cols(self):
        self.reward_col = 'Reward'
        self.reward_avg_col = 'Reward_Average'
        self.reward_var_col = 'Reward_Variance'
        self.ci_upper_col = 'Reward_Upper_CI'
        self.ci_lower_col = 'Reward_Lower_CI'
        self.cost_col = 'Cost'
        self.count_col = 'Count'
        self.context_col = 'Contexts'
        self.action_col = 'Action'
        self.control_col = 'IsControl'
        self.exploit_col = 'IsExploitAction'
        self.lasttime_col = 'LastTimestamp'
        self.exp_col = 'Exp'
        self.date_col = 'Date'
        self.date_format = '%Y-%m-%d'

    def prep_path(self):
        if not os.path.exists(self.output_folder):
            os.makedirs(self.output_folder)

    def complete_config_dates(self, config_exps):
        for exp in config_exps.keys():
            if 'start_date' not in config_exps[exp]:
                config_exps[exp]['start_date'] = datetime.date.min.strftime(self.date_format)
            if 'end_date' not in config_exps[exp]:
                config_exps[exp]['end_date'] = (datetime.date.max - datetime.timedelta(days=1)).strftime(self.date_format)
        return config_exps

    def complete_config(self, config_exps):
        for exp in config_exps.keys():
            config_exps[exp]['action_label_key'] = config_exps[exp].get('action_label_key', None)
            config_exps[exp]['context_feature_namespace'] = config_exps[exp].get('context_feature_namespace', None)
            config_exps[exp]['default_action_index'] = config_exps[exp].get('default_action_index', 0)
            config_exps[exp]['sample_match'] = config_exps[exp].get('sample_match', None)
        return config_exps

    def get_dsjson_files(self, config):
        dsjson_files = []
        data_folder =config['data_folder']
        for path, subdirs, files in os.walk(data_folder):
            for fname in files:
                if fname.endswith('.json'):
                    dsjson_files.append(os.path.join(path, fname))
        return dsjson_files

    def check_key_info(self, fjson):
        key_info = ['_label_cost', '_label_probability', '_label_Action', '_labelIndex', 'Timestamp', 'a', 'c', 'p']
        check_result = all([x in fjson.keys() for x in key_info])
        return check_result

    def check_time(self, fjson, config):
        start_time = datetime.datetime.strptime(config['start_date'], self.date_format)
        end_time = datetime.datetime.strptime(config['end_date'], self.date_format) + datetime.timedelta(days=1)
        timestamp = dateutil.parser.parse(fjson['Timestamp'], ignoretz=True)
        if (timestamp>=start_time) & (timestamp<=end_time):
            return timestamp
        else:
            return False

    def control_logic(self, dsjson, config):
        if 'control_identifier' in config:
            logic = []
            for k, v in config['control_identifier'].items():
                logic.append(dsjson[k] == v)
            logic_bool = all(logic)
        else:
            logic_bool = dsjson['_labelIndex'] == config['default_action_index']
        return logic_bool
        
    def parse_others(self, dsjson, config, timestamp):
        data = {}
        data[self.cost_col] = dsjson['_label_cost']
        data[self.reward_col] = -1 * data[self.cost_col]
        data[self.control_col] = self.control_logic(dsjson, config)
        data[self.action_col] = reduce(operator.getitem, config['action_label_key'], dsjson['c']['_multi'][dsjson['_labelIndex']])
        data[self.exploit_col] = dsjson['p'][0] == np.max(dsjson['p'])
        data[self.lasttime_col] = timestamp
        return data

    def parse_context(self, dsjson, config):
        context_data = {}
        for ns in config['context_feature_namespace']:
            if isinstance(ns, str):
                for f in dsjson['c'][ns]:
                    context_data.update(f)
            elif isinstance(ns, list):
                f = [x for x in dsjson['c'][ns[0]] if ns[1] in x][0]
                context_data.update(f)
            else:
                raise ValueError('context_feature_namespace must be a list of strings or a list of list')
        return context_data

    def parse_dsjson(self, file, config):
        data_context = []
        data_others = []
        with open(file, 'r') as f:
            nline = 0
            for l in f.readlines():
                nline = nline + 1
                try:
                    fjson = json.loads(l)
                except json.JSONDecodeError:
                    warnings.warn('Skip a record with invalid Json Format in file {0} line {1}'.format(file, nline))
                    continue
                # Check key information
                if self.check_key_info(fjson):
                    # Check time
                    timestamp = self.check_time(fjson, config)
                    if timestamp:
                        # Parse data
                        data_context.append(self.parse_context(fjson, config))
                        data_others.append(self.parse_others(fjson, config, timestamp))
        return data_context, data_others

    def process_dsjson(self, dsjson_files, config):
        # Process files
        args = [a for a in itertools.product(dsjson_files, [config])]
        p = multiprocessing.Pool()
        results = p.starmap_async(self.parse_dsjson, args)
        p.close()
        p.join()
        # Split context and others
        data_context = []
        data_others = []
        results_list = results.get()
        for i in range(len(results_list)):
            data_context.extend(results_list[i][0])
            data_others.extend(results_list[i][1])
        return data_context, data_others

    def ci(self, x, ci_multiple=1.96):
        return ci_multiple*np.std(x)/np.sqrt(len(x))

    def read_df(self, econfig):
        # Read and parse files
        dsjson_files = self.get_dsjson_files(econfig)
        data_context, data_others = self.process_dsjson(dsjson_files, econfig)
        df_context = pd.DataFrame(data_context)
        df_others = pd.DataFrame(data_others)
        df = pd.concat([df_context, df_others], axis=1)
        # Duplicate control as treatment if no real control group is included in the experiment -- when 'control_identifier' is defined.
        if 'control_identifier' not in econfig:
            df_control = df.loc[df[self.control_col]==True].copy()
            df_control[self.control_col] = False
            df = df.append(df_control, ignore_index=False)
        df.loc[df[self.control_col]==True, self.exploit_col] = False
        return df, list(df_context.columns)

    def process_data(self, exp, df, features):
        # Aggregated data
        info_exp = defaultdict(dict)
        info_exp['s_context_action']['df'], info_exp['s_context_action']['features'] = self.agg_data(exp, df, features, by_context=True, by_action=True)
        info_exp['s_context']['df'], info_exp['s_context']['features'] = self.agg_data(exp, df, features, by_context=True, by_action=False)
        info_exp['s_all']['df'], info_exp['s_all']['features'] = self.agg_data(exp, df, features, by_context=False, by_action=False)
        # Dates
        dates_in_df = pd.to_datetime(info_exp['s_all']['df'][self.date_col].unique())
        last_date = max(dates_in_df).strftime(self.date_format)
        info_exp['time_range'] = [dates_in_df.min().strftime(self.date_format), dates_in_df.max().strftime(self.date_format), last_date]
        return info_exp

    def format_agg_df(self, df_agg):
        df_agg.columns = [self.reward_avg_col, self.reward_var_col, 'ci', self.count_col, self.lasttime_col]
        df_agg[self.ci_upper_col] = df_agg[self.reward_avg_col] + df_agg['ci']
        df_agg[self.ci_lower_col] = df_agg[self.reward_avg_col] - df_agg['ci']
        df_agg.reset_index(inplace=True)
        return df_agg

    def agg_data(self, exp, df, features, by_context, by_action):
        df_exp = df.copy()
        df_exp[self.exp_col] = exp
        df_exp[self.count_col] = 1
        df_exp[self.date_col] = pd.to_datetime(df_exp[self.lasttime_col]).dt.date.astype(str)
        df_exp[self.control_col] = df_exp[self.control_col].map({True: 'Control', False: 'Treatment'})
        str_cols = [self.action_col, self.exploit_col] + features
        for c in str_cols:
            df_exp[c] = df_exp[c].astype(str)
        if by_action == False:
            df_exp[self.action_col] = 'All'
            df_exp[self.exploit_col] = 'All'
        if by_context == False:
            df_exp[self.context_col] = 'All'
            features = [self.context_col]
        agg_group = [self.exp_col] + features + [self.control_col, self.action_col, self.exploit_col, self.date_col]
        aggs = {self.reward_col: ['mean', 'var', self.ci], self.count_col: 'sum', self.lasttime_col: 'max'}
        df_agg = df_exp.groupby(agg_group).agg(aggs)
        df_agg = self.format_agg_df(df_agg)
        return df_agg, features
 
    def add_cum_cols(self, df_wide):
        for g in self.groups:
            df_wide['Cum_'+self.count_col, g] = df_wide[self.count_col, g].cumsum()
            df_wide['Cum_'+self.reward_avg_col, g] = 1.0 * df_wide['mu_n', g].cumsum() / df_wide['Cum_'+self.count_col, g]
            df_wide['Cum_'+self.reward_var_col, g] = 1.0 * (df_wide['s2_n_1', g].cumsum() + df_wide['mu2_n', g].cumsum() - df_wide['Cum_'+self.count_col, g] * (df_wide['Cum_'+self.reward_avg_col, g]**2)) / (df_wide[self.count_col, g] - 1).cumsum()
            if self.ci_std_mean:
                ci95 = 1.96*((df_wide['Cum_'+self.reward_var_col, g]/df_wide['Cum_'+self.count_col, g])**1/2)
            else:
                ci95 = 1.96*((df_wide['Cum_'+self.reward_var_col, g])**1/2)
            df_wide['Cum_'+self.ci_upper_col, g] = df_wide['Cum_'+self.reward_avg_col, g] + ci95
            df_wide['Cum_'+self.ci_lower_col, g] = df_wide['Cum_'+self.reward_avg_col, g] - ci95
        return df_wide

    def generate_report(self):
        self.set_plot_style()
        html_template = self.set_html_template()
        exp_data = {}
        for exp, econfig in self.config_exps.items():
            print('='*50)
            print('>>> Reading data for {0}...'.format(exp))
            df, features = self.read_df(econfig)
            print('>>> Generating Report')
            info_exp = self.process_data(exp, df, features)
            info_exp, pic_names = self.summarize_exp(exp, info_exp)
            html_exp = self.edit_html(exp, info_exp, html_template, pic_names)
            html_outpath = self.export_html(exp, info_exp['time_range'], html_exp)    
            print('>>> Report saved to {0}'.format(html_outpath))
            excluded_log_path = self.log_excluded(exp, info_exp['time_range'], info_exp)
            exp_data[exp] = info_exp
        return exp_data

    def summarize_exp(self, exp, info_exp):
        tmp_pic_folder = self.prep_pic(exp)
        pic_names = []
        for s in ['s_context', 's_all']:
            df_wide = self.reshape_data(info_exp[s])
            info_exp[s]['table_summary'], info_exp[s]['excluded'] = self.generate_summary_table(df_wide, info_exp, s)
            tmp_pic_names = self.plot_trends(df_wide, info_exp, s, tmp_pic_folder)
            pic_names = pic_names + tmp_pic_names
        return info_exp, pic_names

    def set_plot_style(self):
        plt.style.use('ggplot')
        matplotlib.rcParams['font.sans-serif'] = self.font_name
        matplotlib.rcParams['font.family'] = self.font_family

    def set_html_template(self):
        with open('report_template.html', 'r') as h:
            html_template = h.readlines()
            html_template = ''.join(html_template)
            html_template = html_template.replace('TBD_FONT_NAME', self.font_name)
            html_template = html_template.replace('TBD_FONT_FAMILY', self.font_family)
        return html_template

    def prep_pic(self, exp):
        tmp_pic_folder = os.path.join(self.output_folder, r'{0}/pic'.format(exp))
        if not os.path.exists(tmp_pic_folder):
            os.makedirs(tmp_pic_folder)
        return tmp_pic_folder

    def reshape_data(self, info):
        df_wide = info['df'].copy()
        self.groups = sorted(df_wide[self.control_col].unique())
        df_wide['mu_n'] = df_wide[self.reward_avg_col]*df_wide[self.count_col]
        df_wide['mu2_n'] = ((df_wide[self.reward_avg_col])**2)*df_wide[self.count_col] 
        df_wide['s2_n_1'] = df_wide[self.reward_var_col]*(df_wide[self.count_col]-1)
        group_cols = [self.exp_col] + info['features']
        groupby_cols = group_cols + [self.date_col, self.control_col]
        df_wide = df_wide.groupby(groupby_cols).mean().unstack(-1).reset_index(-1)
        df_wide = df_wide.groupby(group_cols).apply(lambda x: self.add_cum_cols(x))
        keep_cols = [x for x in df_wide.columns.levels[0] if any([c in x for c in [self.date_col, self.count_col, self.reward_avg_col, self.reward_var_col, self.ci_lower_col, self.ci_upper_col]])]
        df_wide = df_wide[keep_cols]
        return df_wide

    def generate_summary_table(self, df_wide, info_exp, s):
        last_date = info_exp['time_range'][2]
        df_last_wide = df_wide.loc[df_wide[self.date_col]==last_date].copy()
        # Add delta and t-test results
        for pre in ['', 'Cum_']:
            df_last_wide[pre+self.reward_avg_col, 'Delta'] = df_last_wide[pre+self.reward_avg_col, 'Treatment'] - df_last_wide[pre+self.reward_avg_col, 'Control']
            mean_diff = np.abs(df_last_wide[pre+self.reward_avg_col, 'Delta'])
            s1 = df_last_wide[pre+self.reward_var_col, 'Control'] / df_last_wide[pre+self.count_col, 'Control']
            s2 = df_last_wide[pre+self.reward_var_col, 'Treatment'] / df_last_wide[pre+self.count_col, 'Treatment']
            sample_std = np.sqrt(s1 + s2)
            degree_fredom = df_last_wide[pre+self.count_col].sum(axis=1)-2
            df_last_wide[pre+self.reward_avg_col, 'p-value'] = np.round(stats.t.sf(mean_diff/sample_std, degree_fredom)*2, 6)
            df_last_wide[pre+self.reward_avg_col, 'sig'] = np.where(df_last_wide[pre+self.reward_avg_col, 'p-value']<self.p_threshold, '*', '')
        context_summary = df_last_wide[[self.count_col, self.reward_avg_col, 'Cum_'+self.count_col, 'Cum_'+self.reward_avg_col]]
        # Get last exploit action
        if s=='s_context':
            context_summary = self.add_exploit_action(context_summary, info_exp)
            summary_included, summary_excluded = self.split_by_results(context_summary, df_wide)
            return summary_included, summary_excluded
        else:
            return context_summary, pd.DataFrame()

    def split_by_results(self, context_summary, df_wide):
        check_n = (context_summary['Cum_'+self.count_col]/df_wide[self.date_col].nunique()>self.min_sample).all(axis=1)
        tmp = context_summary[check_n].copy()
        tmp['abs_cum_delta'] = abs(tmp['Cum_'+self.reward_avg_col]['Delta'])
        tmp = tmp.sort_values('abs_cum_delta', ascending=False)
        tmp_pos_sensitive = tmp.loc[tmp['Cum_'+self.reward_avg_col]['Delta']>0].head(self.top_n)
        tmp_neg_sensitive = tmp.loc[tmp['Cum_'+self.reward_avg_col]['Delta']<0].head(self.top_n)
        tmp_included = tmp_pos_sensitive.append(tmp_neg_sensitive)
        context_summary_included = context_summary.loc[[x for x in list(tmp_included.index.values)]]
        context_summary_excluded = context_summary.loc[[x for x in context_summary.index.values if x not in list(tmp_included.index.values)]]
        return context_summary_included, context_summary_excluded

    def add_exploit_action(self, context_summary, info_exp):
        last_date = info_exp['time_range'][2]
        df_action = info_exp['s_context_action']['df'].copy()
        df_exploit = df_action.loc[(df_action[self.date_col]==last_date)&(df_action[self.exploit_col]=='True')]
        df_exploit_last = df_exploit[df_exploit.groupby(context_summary.index.names)[self.lasttime_col].transform(max) == df_exploit[self.lasttime_col]]
        df_exploit_last.set_index(context_summary.index.names, inplace=True)
        df_exploit_last = df_exploit_last[[self.action_col]]
        df_exploit_last.columns = pd.MultiIndex.from_product([['Last'], ['Exploit Action']])
        context_summary = pd.merge(context_summary, df_exploit_last, left_index=True, right_index=True, how='inner')
        return context_summary

    def plot_trends(self, df_wide, info_exp, s, tmp_pic_folder):
        tmp_pic_names = []
        time_range = info_exp['time_range']
        for i in set(df_wide.index.values):
            # Skip the excluded ones
            if i in set(info_exp[s]['excluded'].index.values):
                continue
            # Title and file name
            title_text = 'Exp {0} - Context: {1}'.format(i[0], ', '.join([x for x in i[1:]]))
            pic_name = '{0}_{1}_{2}-{3}.png'.format(i[0], ''.join([x for x in i[1:]]), time_range[0], time_range[1])
            pic_path = os.path.join(tmp_pic_folder, pic_name)
            fig, axs = plt.subplots(1, 3, figsize=(16,4), sharex=True)
            # Plot Count 
            df_count = df_wide.loc[i, [self.date_col, self.count_col]].copy()
            df_count[self.date_col] = pd.to_datetime(df_count[self.date_col])
            df_count.set_index([self.date_col], inplace=True)
            df_count[self.count_col].plot(ax=axs[0], marker='.', x_compat=True)
            axs[0].set_title('Daily Count')
            axs[0].legend(loc='upper right', framealpha=0.4)
            # Plot Reward
            for p, pre in enumerate(['', 'Cum_']):
                dfi = df_wide.loc[i, [self.date_col, pre+self.reward_avg_col, pre+self.ci_upper_col, pre+self.ci_lower_col]]
                dfi[self.date_col] = pd.to_datetime(dfi[self.date_col])
                dfi.columns = dfi.columns.map('|'.join).str.strip('|')
                df_plot = dfi.set_index([self.date_col])
                reward_lines = [x for x in df_plot.columns if self.reward_avg_col in x]
                axs[p+1].plot(df_plot[reward_lines], marker='.')
                for g in self.groups:
                    group_band = [x for x in df_plot.columns if x not in reward_lines and g in x]
                    axs[p+1].fill_between(df_plot.index, df_plot[group_band[0]], df_plot[group_band[1]], alpha=0.2, label=g)
                subtitle = 'Daily Reward' if pre=='' else 'Cumulative Reward'
                axs[p+1].set_title(subtitle)
                axs[p+1].legend(loc='upper right', framealpha=0.4)
            # Formats
            axs[0].xaxis.set_major_locator(MultipleLocator(df_count.shape[0]//8))
            axs[0].xaxis.set_major_formatter(mdates.DateFormatter('%m/%d/%Y'))
            fig.autofmt_xdate(rotation=30, ha='right')
            fig.suptitle(title_text, fontsize=18)
            fig.tight_layout(rect=[0, 0.03, 1, 0.9])
            if os.path.isfile(pic_path):
                os.remove(pic_path) 
            plt.savefig(pic_path, dpi=200)
            plt.close(fig)
            tmp_pic_names.append(pic_name)
        return tmp_pic_names

    def edit_html(self, exp, info_exp, html_template, pic_names):        
        # Titles
        date_min = info_exp['time_range'][0]
        date_max = info_exp['time_range'][1]
        html_exp = html_template
        html_exp = html_exp.replace('TBD_TITLE', 'Experiment {0} - Context Explorer'.format(exp))
        html_exp = html_exp.replace('TBD_DATES', '{0} - {1}'.format(date_min, date_max))
        html_exp = html_exp.replace('TBD_EXPID', str(exp))
        # Style
        html_exp = html_exp.replace('TBD_FONT_NAME', self.font_name)
        html_exp = html_exp.replace('TBD_FONT_FAMILY', self.font_family)
        # [1] Overall - Trend
        p1_pics = [p for p in pic_names if p=='{0}_{1}_{2}-{3}.png'.format(exp, 'All', date_min, date_max)]
        p1_pics = ''.join(['<img src="pic\{0}" width="1200"><br>'.format(p) for p in p1_pics])
        html_exp = html_exp.replace('TBD_OverallPlot', p1_pics)
        counts = info_exp['s_all']['table_summary']['Cum_'+self.count_col].values[0]
        if abs(counts[0]-counts[1])/max(counts)>0.2:
            html_exp = html_exp.replace(
                'TBD_Overall_Warning', 
                'WARNING: Sample size mismatch between control and treatment. Overall results on this page can be misleading. Please refer to Context Summary and Context Trend for detailed results. <br><br>')
        else:
            html_exp = html_exp.replace('TBD_Overall_Warning', '')
        # [2] Context - Latest and Cumulative Performance
        p2_table = info_exp['s_context']['table_summary'].copy()
        html_exp = html_exp.replace('TBD_NIDX', str(len(p2_table.index.names)))
        p2_table.reset_index(col_level=1, col_fill='Context', inplace=True)
        p2_table.columns.names = [None, None]
        p2_table = p2_table.to_html(index=False)
        html_exp = html_exp.replace('TBD_ContextTable', p2_table)
        html_exp = html_exp.replace('TBD_LASTDATE', info_exp['time_range'][2])
        # [3] Context - Trend
        p3_pics = sorted([p for p in pic_names if (p.split('_')[1]!='All') and (p.split('_')[-1]=='{0}-{1}.png'.format(date_min, date_max))])
        p3_pics = ''.join(['<img src="pic\{0}" width="1200"><br>'.format(p) for p in p3_pics])
        html_exp = html_exp.replace('TBD_ContextPlot', p3_pics)    
        return html_exp

    def export_html(self, exp, dates, html_exp):
        html_outpath = os.path.join(*[self.output_folder, exp, 'Context_Explorer_{0}_{1}-{2}.html'.format(exp, dates[0], dates[1])])
        with open(html_outpath, 'w') as o:
            o.write(html_exp)
        return html_outpath

    def log_excluded(self, exp, dates, info_exp):
        excluded_log_path = os.path.join(*[self.output_folder, exp, 'excluded_contexts_{0}_{1}-{2}.csv'.format(exp, dates[0], dates[1])])
        info_exp['s_context']['excluded'].to_csv(excluded_log_path)
        return excluded_log_path

def print_process(iteration, total, prefix='', suffix='', decimals=0, length=30, empty='-', fill='|'):
    percent = ("{0:." + str(decimals) + "f}").format(100 * (iteration / float(total)))
    filledLength = int(round(length * iteration / total))
    bar = fill * filledLength + empty * (length - filledLength)
    print('\r%s |%s| %s%% %s' % (prefix, bar, percent, suffix), end = '\r')
    if iteration == total: 
        print()

