import csv
import datetime
import itertools
import json
import os
import random
import uuid
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
plt.style.use('ggplot')
from scipy.stats import norm, ttest_ind

def update_params(params):
    # Data parameters
    params['random_state'] = params.get('random_state', 1)
    params['reward_diff'] = params['reward_dense_range'][1]-params['reward_dense_range'][0]
    params['sd'] = params['reward_diff']/(len(params['actions'])+1)
    params['reward_lower_bound'] = params['reward_dense_range'][0] + 2*params['sd']
    params['reward_upper_bound'] = params['reward_dense_range'][1]
    params['contexts_unique'] = list(itertools.product(*params['contexts'].values()))
    params['p_value'] = params.get('p_value', 0.001)
    params['increase_winning_margin'] = params.get('increase_winning_margin', 0)
    params['center'] = params.get('center', True)
    # Model parameters
    params['model_parameters']['default_action_index'] = params['model_parameters'].get('default_action_index', 0)
    params['model_parameters']['add_control_group'] = params['model_parameters'].get('add_control_group', False)
    # VW Commands
    params['vw_commands']['exploration_policy'] = params['vw_commands'].get('exploration_policy', '--epsilon 0.3')
    params['vw_commands']['cb_type'] = params['vw_commands'].get('cb_type', 'ips')
    params['vw_commands']['interactions'] = params['vw_commands'].get('interactions', '--interactions iFFF')
    params['vw_commands']['learning_rate'] = params['vw_commands'].get('learning_rate', 0.001)
    params['vw_commands']['other_commands'] = params['vw_commands'].get('other_commands', '')
    # Files
    log_path = os.path.join(params['output_folder'], 'logs')
    if not os.path.exists(log_path):
        os.makedirs(log_path)
    params['df_file'] = os.path.join(params['output_folder'], '{0}_simulated_dataset.csv'.format(params['dataset_name']))
    params['context_dsjson_path'] = os.path.join(params['output_folder'], '{0}_unique_context.json'.format(params['dataset_name']))
    params['context_pred_path'] = os.path.join(params['output_folder'], '{0}_unique_context_pred.txt'.format(params['dataset_name']))
    params['batch_dsjson_path'] = os.path.join(log_path, '{0}_batch_input.json'.format(params['dataset_name']))
    params['model_file'] = os.path.join(params['output_folder'], '{0}_model.model'.format(params['dataset_name']))
    params['pred_file'] = os.path.join(params['output_folder'], '{0}_unique_context_pred.txt'.format(params['dataset_name']))
    return params

def fit_distribution(ss, reward_range):
    xt = plt.xticks()[0]
    xmin, xmax = reward_range[0], reward_range[1]
    lnspc = np.linspace(xmin, xmax, len(ss))
    m, sd = norm.fit(ss) # get mean and standard deviation  
    pdf_g = norm.pdf(lnspc, m, sd) # now get theoretical values in our interval  
    return lnspc, pdf_g

def summarize_context_action(reward_mean, ttest_df, p_value):
    reward_mean_sorted = reward_mean['avg reward'].sort_values(ascending=False)
    action_sorted = list(reward_mean_sorted.index)
    action_best = []
    for i, a in enumerate(action_sorted):
        action_best.append(a)
        try:
            a_1 = action_sorted[i+1]
        except IndexError:
            continue
        if ttest_df.loc[a, a_1]<p_value:
            break
    return reward_mean_sorted, action_best

def generate_data(**kargs):
    random.seed(kargs['random_state'])
    df = pd.DataFrame()
    context_action_stats = {}
    for c in kargs['contexts_unique']:
        context_action_stats[c] = {}
        for a in kargs['actions']:
            mu = random.uniform(kargs['reward_lower_bound'], kargs['reward_upper_bound'])
            n = kargs['context_action_size']
            tmp_rand = np.random.normal(mu, kargs['sd'], n)
            context_action_stats[c][a] = [mu, kargs['sd'], n]
            tmp_data = pd.DataFrame(tmp_rand, columns=['reward'], 
                                    index=pd.MultiIndex.from_tuples([c]*n, names=kargs['contexts'].keys()))
            tmp_data.insert(0, 'action', a)
            df = df.append(tmp_data)
    df['ConfigId'] = 'P-E-TEST-'+ df['action'].astype('str')
    return df, context_action_stats

def summarize_vw(input_file, config_json, context_actions):
    vw_summary = pd.read_excel(input_file, 
                               dtype={'bandit_sig': bool, 
                                      'default_action': 'str', 
                                      'optimal_action': 'str', 
                                      'bandit_action': 'str'})
    vw_summary.columns = [x.replace('context.', '') for x in vw_summary.columns]
    vw_summary.rename(columns={'optimal_action': 'ttest_action'}, inplace=True)
    vw_summary.set_index(config_json["FEATURE_COLUMNS"], inplace=True)
    idx = vw_summary.columns.get_loc("ttest_action")
    vw_summary.insert(idx, 'GT_best_action', np.nan)
    for c, a in context_actions.items():
        vw_summary.loc[c, 'GT_best_action'] = str(a['action_best'])    
    return vw_summary

def calculate_correct_rate(s, col_ground_truth):
    tmp = s.reset_index()
    pred_col = [x for x in s.columns if x!=col_ground_truth]
    GT = [[str(c) for c in eval(x)] for x in s[col_ground_truth]]
    preds = s[pred_col].values
    tmp['correct_rate'] = [int(preds[i] in GT[i]) for i in range(len(preds))]
    correct_rate = pd.DataFrame(tmp.groupby(s.index.names)['correct_rate'].mean())
    return correct_rate

def summarize_dataset(df, params, show_results=True):
    actions = params['actions']
    context_actions = {}
    for c in params['contexts_unique']:
        context_actions[c] = {}
        # Context DF
        df_c = df.loc[c].copy()
        reward_mean = df_c.groupby('action').mean()
        reward_mean.columns = ['avg reward']
        # Prep TTEST
        ttest_df = pd.DataFrame(np.nan, columns=actions, index=actions)
        # Prep Plot
        if show_results:
            fig = plt.figure(figsize=(15, 4))
        # Loop through actions
        for a1 in actions:
            s_a1 = df_c.loc[df_c['action']==a1, 'reward']
            # T-test
            for a2 in actions:
                if a1!=a2 and np.isnan(ttest_df.loc[a1, a2]):
                    s_a2 = df_c.loc[df_c['action']==a2, 'reward']
                    p = ttest_ind(s_a1, s_a2)[1]
                    ttest_df.loc[a1, a2] = p
                    ttest_df.loc[a2, a1] = p
            # Plot
            if show_results:
                plt.subplot(121)
                pltx, plty = fit_distribution(s_a1, params['reward_range'])
                plt.plot(pltx, plty, label=a1)
        # Show distribution results
        if show_results:
            plt.legend(title='Actions')
            plt.title('Context {0}'.format(c))
            plt.subplot(122)
        # Best actions
        context_actions[c]['action_rewards'], context_actions[c]['action_best'] = summarize_context_action(reward_mean, ttest_df, params['p_value'])
        if show_results:
            ttest_df = ttest_df.append(reward_mean.transpose())
            best_actions = []
            for a in ttest_df.columns:
                if a in context_actions[c]['action_best']:
                    best_actions.append('Best')
                else:
                    best_actions.append('')
            best_actions_df = pd.DataFrame([best_actions], index=['Best Actions'], columns=ttest_df.columns)
            # Show table
            ttest_colors = list(np.where(ttest_df.values<params['p_value'], 'powderblue', 'white'))
            ttest_colors.append(['white']*ttest_df.shape[1])
            ttest_df = ttest_df.round(4)
            ttest_df = ttest_df.append(best_actions_df)
            plt.table(cellText=ttest_df.values,
                      rowLabels=ttest_df.index, colLabels=ttest_df.columns,
                      cellColours=ttest_colors, 
                      colWidths = [1/(len(actions)+1)]*len(actions),
                      cellLoc = 'center', rowLoc = 'center', loc='center')
            plt.axis('off')
            plt.title('Best Action(s) and p-values')
            plt.show()
    return context_actions

def increase_lead(df, context_actions, add_value=0.1):
    for k, v in context_actions.items():
        targets = df.index.isin([k]) & (df['action'].isin(v['action_best']))
        df.loc[targets, 'reward'] = df.loc[targets, 'reward'] + add_value
    return df

def binary_reward(df, context_actions):
    df['reward'] = 0
    for k, v in context_actions.items():
        targets = df.index.isin([k]) & (df['action'].isin(v['action_best']))
        df.loc[targets, 'reward'] = 1
    return df

def highlight_suboptimal(s, best, columns):
    if s.name not in columns:
        return ['']*len(s)
    is_subopt = []
    for i in range(len(s)):
        tmp_subopt = False
        if s[i] != best[i]:
            tmp_subopt = True
        is_subopt.append(tmp_subopt)
    return ['background-color: yellow' if v else '' for v in is_subopt]

def highlight_optimal(s, is_minimization):
    if is_minimization:
        is_opt = s == s.min()
    else:
        is_opt = s == s.max()
    return ['background-color: lightgreen' if v else '' for v in is_opt]

def transform_dsjson(df, context_cols, reward_col, action_col, actions, is_minimization, other_values=None):
    action_list = [x+1 for x in range(len(actions))]
    df_json = pd.DataFrame(index=df.index)
    df_json['left_brace'] = '{'
    df_json['label_cost'] = '"_label_cost":'
    df_json['cost'] = df[reward_col] if is_minimization else -1*df[reward_col]
    df_json['label_probability'] = ',"_label_probability":'
    df_json['probability'] = 0
    df_json['label_Action'] = ',"_label_Action":'
    df_json['action'] = df[action_col].map({a: i+1 for i, a in enumerate(actions)})
    df_json['labelIndex'] = ',"_labelIndex":'
    df_json['aindex'] = df[action_col].map({a: i for i, a in enumerate(actions)})
    eventid = df_json.apply(lambda x : uuid.uuid4().hex, axis=1)
    df_json['o'] = ',"o":[{"EventId":"EventId_' + eventid + '","v":' + df_json['cost'].astype(str) + '}]'
    df_json['Timestamp'] = ',"Timestamp":'    
    try:
        ni = int(df['n_iteration'][0])
    except KeyError:
        ni = 0
    df_json['time'] = '"' + (datetime.datetime.utcnow() + datetime.timedelta(days=ni)).isoformat() + 'Z"'
    df_json['VE'] = ',"Version":"1","EventId":"EventId_' + eventid + '",'
    df_json['a'] = '"a":' + df_json['aindex'].apply(lambda x: swap_selection(x, action_list))
    add_context(df, df_json, context_cols)
    context_multi = [{'id': {str(x): 1}} for x in range(len(action_list))]
    df_json['multi'] = '"_multi":' + json.dumps(context_multi) + ' },'
    if 'prob_list' not in df.columns:
        p = round(1.0/len(actions), 4)
        df['prob_list'] = [[p]*len(actions)]*df.shape[0]
    df_json['prob_list'] = df['prob_list']
    p_swarpped = df_json.apply(lambda x: swap_selection(x['aindex'], x['prob_list']), axis=1)
    df_json.drop(columns=['prob_list'], inplace=True)
    df_json['p'] = '"p":' + p_swarpped + ','
    df_json['probability'] = p_swarpped.apply(lambda x: eval(x)[0])
    df_json['m'] = '"m": "v1"'
    if other_values is not None:
        df_json['other_values'] = ',' + df[other_values]
    df_json['right_brace'] = '}'
    df_json['output_json'] = df_json.astype(str).sum(axis=1).str.replace(' ', '')
    output_json = df_json['output_json'].to_frame()
    return output_json

def add_context(df, df_json, context_cols):
    df_json['c'] = ',"c": {"Features":['
    for i, x in enumerate(context_cols):
        if df[x].dtype.name.startswith(('float', 'int')):
            df_json['c'] = df_json['c'] + '{"' + x + '": ' + df[x].astype(str) + '}' 
        else:
            df_json['c'] = df_json['c'] + '{"' + x + '": "' + df[x].astype(str) + '"}'
        if i!=len(context_cols)-1:
            df_json['c'] = df_json['c'] + ','
        else:
            df_json['c'] = df_json['c'] + '],'

def swap_selection(select_idx, full_list):
    swapped = full_list.copy()
    swapped[select_idx] = full_list[0]
    swapped[0] = full_list[select_idx]
    swapped = str(swapped)
    return swapped

def export_dsjson(output_json, batch_file_path):
    output_json.to_csv(batch_file_path, index=False, header=False, sep='\t', quoting=csv.QUOTE_NONE, escapechar=' ')
    
def load_pred_context(pred_file, df_contexts, context_cols, action_mapping):
    preds = []
    with open(pred_file, 'r') as f:
        for l in f.readlines():
            if l!='\n':
                preds.append(eval('{' + l + '}'))
    df_contexts_pred = df_contexts[context_cols].copy()
    df_contexts_pred['prob']= preds
    df_contexts_pred['prob_list'] = df_contexts_pred['prob'].apply(lambda x: [x[k] for k in sorted(list(x.keys()))])
    df_contexts_pred['exploit_action'] = df_contexts_pred['prob'].apply(lambda x: [k for k, v in x.items() if v==max(x.values())][0])
    df_contexts_pred['exploit_action'] = df_contexts_pred['exploit_action'].map(action_mapping).astype(str)
    return df_contexts_pred

def choose_action(df_batch, pred_context, action_col, action_mapping, balance_default=False, default_action_index=0):
    context_cols = list(df_batch.columns)
    df_batch = pd.merge(df_batch, pred_context, left_on=context_cols, right_on=context_cols, how='left')
    df_batch['action_idx'] = df_batch['prob'].apply(lambda x: np.random.choice(list(x.keys()), 1, p=list(x.values())/np.sum(list(x.values())))[0])
    df_batch['action_prob'] = df_batch.apply(lambda x: x['prob'][x['action_idx']], axis=1)
    df_batch[action_col] = df_batch['action_idx'].map(action_mapping).astype(str)
    df_batch.set_index(context_cols + [action_col], inplace=True)
    return df_batch

def get_reward(df_batch, df, reward_col):
    rewards = []
    for idx in df_batch.index:
        try:
            rewards.append(np.random.choice(df.loc[idx, reward_col].values))
        except KeyError:
            rewards.append(np.nan)
    df_batch[reward_col] = rewards
    df_batch.dropna(inplace=True)
    return df_batch

def get_unique_context(df_summary, action_col, reward_col, is_minimization):
    context_cols = list(df_summary.index.names)
    df_contexts = df_summary.copy()
    df_contexts.columns.name = ''
    if is_minimization:
        df_contexts[action_col] = df_contexts.idxmin(axis=1)
        df_contexts[reward_col] = df_contexts.min(axis=1)
    else:
        df_contexts[action_col] = df_contexts.idxmax(axis=1)
        df_contexts[reward_col] = df_contexts.max(axis=1)    
    df_contexts = df_contexts.reset_index()[context_cols + [action_col, reward_col]]
    return df_contexts

def select_data(i, df, df_contexts, configs, action_mapping, context_cols, action_col, reward_col):
    if i==0:
        df_batch = df.sample(configs['model_parameters']['batch_size_initial']).copy().reset_index()
        df_batch['action_prob'] = round(1/len(configs['actions']), 4)
        df_batch['prob_list'] = [[df_batch['action_prob'][0]]*len(configs['actions'])]*df_batch.shape[0]
    else:
        pred_context = load_pred_context(configs['pred_file'], df_contexts, context_cols, action_mapping)
        df_batch = df.sample(configs['model_parameters']['batch_size']).copy().reset_index()[context_cols]
        df_batch = choose_action(df_batch, pred_context, action_col, action_mapping)
        df_batch = get_reward(df_batch, df, reward_col)
        df_batch = df_batch.reset_index()
    df_batch = df_batch[context_cols + [action_col, reward_col, 'action_prob', 'prob_list']]
    df_batch['n_iteration'] = i
    if configs['model_parameters']['add_control_group']:
        df_batch['control_identifier'] = '"_group": "treatment"'
        control_identifier = 'control_identifier' 
    else:
        control_identifier = None
    return df_batch, control_identifier

def get_regrets(trajectory, df_contexts, context_cols, reward_col, exploration_policy, is_minimization):
    regret = pd.merge(trajectory, df_contexts, left_on=context_cols, right_on=context_cols, how='left', suffixes=['', '_optimal'])
    multiplier = -1.0 if is_minimization else 1.0
    regret['regret'] = multiplier * (regret[reward_col+'_optimal'] - regret[reward_col])
    regret['exploration'] = exploration_policy
    return regret

def plot_regrets(regret, groups, cumulate=False, rolling_window=10):
    regret_avg = regret.groupby(groups)['regret'].mean().reset_index(-1)
    fig = plt.figure(figsize=(8,4))
    ax1 = fig.add_subplot(111)
    plot_contexts = regret_avg.index.unique().values
    for c in plot_contexts:
        plot_data = regret_avg.loc[c, ['n_iteration', 'regret']].set_index('n_iteration')
        if cumulate:
            plot_data = plot_data.cumsum()
        else:
            plot_data = plot_data.rolling(rolling_window, min_periods=1).mean()
        plot_data.plot(label=c, ax=ax1)
    plt.title('Average Regret by Iteration')
    plt.legend(plot_contexts, loc="upper right", framealpha=0.2, fontsize='small')
    plt.xlabel('Number of Iterations')
    plt.ylabel('Average Regret by Iteration (rolling window = 10)')
    plt.show()

def init_plot(iterations):
    fig, ax = plt.subplots(1, 1, figsize=(8, 3))
    plt.title('Accuracy by Iteration')
    ax.set_xlabel('Iteration')
    ax.set_ylabel('Accuracy')
    ax.set_xlim(0, iterations)
    ax.set_ylim(0, 1.03)
    return fig, ax

def plt_dynamic(fig, ax, y):
    ax.plot(y, c='c')
    fig.canvas.draw()

def add_control_identifier(df_batch):
    df_batch['control_identifier'] = '"_group": "treatment"'
    return df_batch

def create_control_logs(i, df, new_name, configs, actions, context_cols, action_col, reward_col):
    new_name_control = new_name.replace('.json', '_control.json')
    if i==0:
        df_batch = df.sample(configs['model_parameters']['batch_size_initial']).copy().reset_index()
    else:
        df_batch = df.sample(configs['model_parameters']['batch_size']).copy().reset_index()[context_cols]
    df_batch[action_col] = actions[configs['model_parameters']['default_action_index']]
    df_batch['action_prob'] = 1/len(actions)
    df_batch['prob_list'] = [[1/len(actions)]*len(actions)]*df_batch.shape[0]
    df_batch.set_index(context_cols+[action_col], inplace=True)
    df_batch = get_reward(df_batch, df, reward_col)
    df_batch = df_batch.reset_index()[context_cols + [action_col, reward_col, 'action_prob', 'prob_list']]
    df_batch['control_identifier'] = '"_group": "control"'
    df_batch['n_iteration'] = i
    df_batch_json = transform_dsjson(df_batch, context_cols, reward_col, action_col, actions, is_minimization=False, other_values='control_identifier')
    export_dsjson(df_batch_json, new_name_control)