import numpy as np
import matplotlib.pyplot as plt
import ds_parse, json, argparse, collections, gzip, os

def update(files, dt_str=13):
    fp_list = ds_parse.input_files_to_fp_list(files)
    l = []
    c_imp = collections.Counter()
    c_clk = collections.Counter()
    c_imp_all = collections.Counter()
    for fp in fp_list:
        bytes_count = 0
        tot_bytes = os.path.getsize(fp)
        for i,x in enumerate(gzip.open(fp, 'rb') if fp.endswith('.gz') else open(fp, 'rb')):
            bytes_count += len(x)
            if (i+1) % 1000 == 0:
                if fp.endswith('.gz'):
                    ds_parse.update_progress(i+1,prefix=fp+' - ')
                else:
                    ds_parse.update_progress(bytes_count,tot_bytes,fp+' - ')
        
            if x.startswith(b'{"_label') and x.strip().endswith(b'}'):
                data = ds_parse.json_cooked(x)
                if data['a'] <= 0:
                    continue
                
                c_imp_all.update([data['ts'][:dt_str]])
                if not data['skipLearn']:
                    c_imp.update([data['ts'][:dt_str]])
                    l.append((data, x.strip()))
                    if float(data['cost']) < 0:
                        c_clk.update([data['ts'][:dt_str]])
        if fp.endswith('.gz'):
            ds_parse.update_progress(i+1,prefix=fp+' - ')
        else:
            ds_parse.update_progress(bytes_count,tot_bytes,fp+' - ')
        print()
                    
    ctr = []
    ts = []
    print('Timestamp (UTC),Clicks,Activated Imp.,CTR,Total Imp.')
    for x in c_imp_all:
        ctr.append(c_clk[x]/max(c_imp[x],1))
        ts.append(x)
        print('{},{},{},{:.2%},{}'.format(x,c_clk[x],c_imp[x],ctr[-1],c_imp_all[x]))
    print()
    return ts,ctr,l

def create_act_d(l):
    act_d = {}
    data = []
    ctr_all = {}
    for i,x in enumerate(l):
        js = json.loads(x[1])
        if i == 0:
            print('These are the actions features from your first event:\n',js['c']['_multi'])
            actions_names_fields = input('\nEnter a (comma separated) list of JSON fields used to extract the action name:').split(',')
            try:
                sep1,sep2 = input('Enter separators to parse the action name string keeping only substring between the separators (comma separated):').split(',')
            except:
                print('Separators not correctly entered - not using separators')
                sep1,sep2 = '',''
            print('Start parsing...')
        
        if max(js['p']) - min(js['p']) > 1e-5:
            model_ind = js['a'][np.argmax(js['p'])]-1
            vw_model = js.get('VWState', {}).get('m', 'N/A')
        else:
            model_ind = -1
            a_mod = None
            vw_model = 'N/A'
        actions = set()
        temp = []
        for j,y in enumerate(js['c']['_multi']):

            ########### Parsing action features to extract name ########
            action_name = y[actions_names_fields[0]]
            for field in actions_names_fields[1:]:
                action_name = action_name[field]
            if sep1:
                action_name = action_name.split(sep1,1)[1]
            if sep2:
                action_name = action_name.split(sep2,1)[0]
            ############################################################
            
            is_firstAction = int(j > 0)
            if action_name not in act_d:
                act_d[action_name] = (len(act_d),[],[])
            if action_name not in actions:
                actions.add(action_name)
                act_d[action_name][is_firstAction+1].append(i)
            if j == js['_labelIndex']:
                a = act_d[action_name][0]
            if j == model_ind:
                a_mod = act_d[action_name][0]
            temp.append(action_name)
            if act_d[action_name][0] not in ctr_all:
                ctr_all[act_d[action_name][0]] = [0,0,0,0,0,action_name,collections.Counter()]
            ctr_all[act_d[action_name][0]][2] += 1
        data.append((a, a_mod, js['_label_cost'], model_ind, js['_label_Action'], js['Timestamp'],temp, vw_model))
        ctr_all[a][1] += 1
        ctr_all[a][4] += 1/js['_label_probability']
        ctr_all[a][-1].update([-js['_label_cost']])
        if js['_label_cost'] != 0:
            ctr_all[a][0] -= js['_label_cost']
            ctr_all[a][3] -= js['_label_cost']/js['_label_probability']

        if (i+1) % 1000 == 0:
            ds_parse.update_progress(i+1,len(l))
    ds_parse.update_progress(i+1,len(l))

    print('\n\nActionId,Rewards,Choosen,Available,Rew. IPS,Choosen IPS,IPS,SNIPS,ActionName')
    for a in range(len(ctr_all)):
        print(','.join(map(str,[a]+ctr_all[a][:-2]+[ctr_all[a][3]/max(ctr_all[a][2],1),ctr_all[a][3]/max(ctr_all[a][4],1)]+[ctr_all[a][-2]])))
    
    print('\nMost Common Rewards')
    rew_list = sorted({x[0] for a in range(len(ctr_all)) for x in ctr_all[a][-1].most_common(10)})
    print(','.join(map(str, ['ActionId']+rew_list)))
    for a in range(len(ctr_all)):
        print(','.join(map(str, [a]+[ctr_all[a][-1][r] for r in rew_list])))
        
    return act_d,data,ctr_all

def plot_act_d(act_d, data, num_ticks=31, plot_rew_zero=False, colors=['C0', 'C1', 'C2', 'C4', 'C6', 'C5', 'C7'], ms1=10, ms2=10):
    vw_model_d = {}
    for i,x in enumerate(data):
        if x[-1] not in vw_model_d:
            vw_model_d[x[-1]] = ([],[])
        if x[1] is not None:
            vw_model_d[x[-1]][0].append(i)
            vw_model_d[x[-1]][1].append(x[1])
        else:
            for a in x[-2]:
                vw_model_d[x[-1]][0].append(i)
                vw_model_d[x[-1]][1].append(act_d[a][0])            

    for i,x in enumerate(vw_model_d):
        if x == 'N/A':
            plt.plot(vw_model_d[x][0], vw_model_d[x][1], "rd", markersize=ms1)
        else:
            plt.plot(vw_model_d[x][0], vw_model_d[x][1], "{}s".format(colors[i%len(colors)]), markersize=ms2)

    for a in act_d:
        plt.plot(act_d[a][1], [act_d[a][0] for _ in range(len(act_d[a][1]))], "C1.")
        plt.plot(act_d[a][2], [act_d[a][0] for _ in range(len(act_d[a][2]))], "y.")
    if plot_rew_zero:
        plt.plot([i for i,x in enumerate(data) if x[2] == 0], [x[0] for x in data if x[2] == 0], "r.", markersize=7, markeredgewidth=1, markeredgecolor='r', markerfacecolor='None')
    plt.plot([i for i,x in enumerate(data) if x[2] < 0], [x[0] for x in data if x[2] < 0], "b.", markersize=7, markeredgewidth=1, markeredgecolor='b', markerfacecolor='None')

    indices = np.linspace(0, len(data)-1, num_ticks, endpoint=True, dtype=int)
    plt.xticks(indices, [data[i][5].split('.')[0][5:-3].replace('T', '\n') for i in indices])
    plt.ylabel('Actions')
    plt.xlabel('Timestamp (UTC)')
    plt.show()
    
    
if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-l','--log_fp', help="data file path (.json or .json.gz format - each line is a dsjson)", required=True)
    parser.add_argument('--plot_rew_zero', help="flag to, in addition to positive rewards, plot also zero rewards", action='store_true')
    
    args = parser.parse_args()
    
    ts,ctr,l = update(args.log_fp)
    act_d,data,ctr_all = create_act_d(l)
    plot_act_d(act_d, data, plot_rew_zero=args.plot_rew_zero)
    
