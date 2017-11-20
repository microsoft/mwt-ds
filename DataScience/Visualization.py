import json, os, time, pickle, sys
import matplotlib.pyplot as plt
import numpy as np
import configparser
import AzureStorageDownloader


# Create dictionary with filename as keys
def parse_logs(raw_stats, files):
    t0 = time.time()
    
    for fp in files:
        if os.path.basename(fp) in raw_stats and time.time()-os.path.getmtime(fp) > 3600:
            continue
        print('Processing: {} - Last modified: {}'.format(fp,os.path.getmtime(fp)))
        
        c2 = {}
        ii = 0
        for line in open(fp, encoding="utf8"):
            ii += 1
            if ii % 10000 == 0:
                print(ii)
            if 'Timestamp' not in line or '_label_cost' not in line:
                continue
            
            try:
                x = json.loads(line.split(',"_multi":[', 1)[0].strip()+'}}')
                
                # Parse datetime string and device
                d = x['Timestamp'][:13]
                if 'DeviceType' in x['c']['OUserAgent']:
                    dev = x['c']['OUserAgent']['DeviceType']
                else:
                    dev = 'N/A'
                
                if d not in c2:
                    c2[d] = {}
                if dev not in c2[d]:
                    c2[d][dev] = [0,0,0]
                if 'ips' not in c2:
                    c2['ips'] = {}
                if d[:10] not in c2['ips']:
                    c2['ips'][d[:10]] = [0,0]
                    
                c2[d][dev][1] += 1
                c2['ips'][d[:10]][1] += 1
                if x['_label_cost'] < 0:
                    c2[d][dev][0] += 1
                    c2[d][dev][2] -= x['_label_cost']
                    if x['_label_Action'] == 1:
                        c2['ips'][d[:10]][0] += -x['_label_cost']/x['_label_probability']
                        
            except Exception as e:
                print('error: {0}'.format(e))

        raw_stats[os.path.basename(fp)] = c2

    print('Log reading time:', time.time()-t0)
    
def convert_pStats_from_hours_to_days(pStats):
    # datetime str format 2017-09-29T12
    pStats_temp = {}
    for x in pStats:
        for dev in x[1]:
            day = x[0][:10]
            if day not in pStats_temp:
                pStats_temp[day] = {}
            if dev not in pStats_temp[day]:
                pStats_temp[day][dev] = list(x[1][dev])
            else:
                for i in range(3):
                    pStats_temp[day][dev][i] += x[1][dev][i]
        
    return [(x,pStats_temp[x]) for x in pStats_temp] 

if __name__ == '__main__':
    
    ################################# PARSE INPUT CMD #########################################################
    kwargs = AzureStorageDownloader.parse_argv(sys.argv)
    container = kwargs['container']
    log_dir = kwargs['log_dir']
    
    ################################# DATA DOWNLOADER #########################################################
    
    if len(sys.argv) > 5:
        AzureStorageDownloader.download_container(**kwargs)
        
    ################################# PARSE LOGS #########################################################

    raw_stats = {}
    pkl_fp = os.path.join(log_dir, 'ds_'+container+'_hours.pickle')
    if os.path.isfile(pkl_fp):
        with open(pkl_fp, 'rb') as pkl_file:
            raw_stats = pickle.load(pkl_file)

    print('raw_stats.keys():',raw_stats.keys())

    files = [x.path for x in os.scandir(os.path.join(log_dir,container)) if x.path.endswith('.json') and '_skip' not in x.name]

    parse_logs(raw_stats, files)
    
    # Update picke file
    with open(pkl_fp, 'wb') as pkl_file:
        pickle.dump(raw_stats, pkl_file)

    # Create dictionary with hours as keys
    stats = {}
    stats_ips = {}
    for fn in raw_stats:
        for h in raw_stats[fn]: 
            if h == 'ips':
                for day in raw_stats[fn]['ips']: 
                    stats_ips.setdefault(day, []).append(raw_stats[fn]['ips'][day])
            else:
                stats.setdefault(h, []).append(raw_stats[fn][h])
    
    ############################ VISUALIZATIONS ##################################################

    for do_by_day in [False, True]:    
        pStats = sorted([(fn,sorted(stats[fn], key=lambda x : sum(x[k][1] for k in x))[-1]) for fn in stats])
        
        if do_by_day:
            pStats = convert_pStats_from_hours_to_days(pStats)
            days = [(i,x[0]) for i,x in enumerate(pStats)]
            pStats_ips = sorted([(h,sorted(stats_ips[h], key=lambda x : x[-1])[-1]) for h in stats_ips])
        else:
            days = [(i,x[0]) for i,x in enumerate(pStats) if 'T12' in x[0]] # Time is in UTC: T12 is 8am EST
            
            smoothing_hours = 3
            # Visualize running average data of Reward/Request over the last 7 days
            plt.figure(1)
            data2 = [[(x[0],sum(y[2] for y in x[1].values())/sum(y[1] for y in x[1].values())) for x in pStats if d[1][:10] in x[0]] for d in days[-7:]]
            [plt.plot([np.mean([x[1] for x in y[i:i+smoothing_hours]]) for i in range(len(y))]) for y in data2]
            legend = plt.legend([y[0][0][:10] for y in data2], loc='best')
            plt.xticks(range(25), list(range(20,24))+list(range(20)))
            plt.title('Reward/Request Ratio over last 7 days (Smoothing '+str(smoothing_hours)+' hours)')
            plt.xlabel('Time of day - EST')
            
            # Visualize running average data of Reward/Request over the last 7 days
            plt.figure(2)
            data2 = [[(x[0],sum(y[2] for y in x[1].values())/sum(y[1] for y in x[1].values())) for x in pStats if d[1][:10] in x[0]] for d in reversed(days[::-7])]
            [plt.plot([np.mean([x[1] for x in y[i:i+smoothing_hours]]) for i in range(len(y))]) for y in data2]
            legend = plt.legend([y[0][0][:10] for y in data2], loc='best')
            plt.xticks(range(25), list(range(20,24))+list(range(20)))
            plt.title('Reward/Request Ratio over same day of the week (Smoothing '+str(smoothing_hours)+' hours)')
            plt.xlabel('Time of day - EST')
            
            plt.figure(3)
            data2 = [[(x[0],sum(y[1] for y in x[1].values())) for x in pStats if d[1][:10] in x[0]] for d in days[-7:]]
            [plt.plot([np.mean([x[1] for x in y[i:i+1]]) for i in range(len(y))]) for y in data2]
            legend = plt.legend([y[0][0][:10] for y in data2], loc='best')
            plt.xticks(range(25), list(range(20,24))+list(range(20)))
            plt.title('Request over last 7 days')
            plt.xlabel('Time of day - EST')
            
            # Visualize running average data of Reward/Request over the last 7 days
            plt.figure(4)
            data2 = [[(x[0],sum(y[1] for y in x[1].values())) for x in pStats if d[1][:10] in x[0]] for d in reversed(days[::-7])]
            [plt.plot([np.mean([x[1] for x in y[i:i+1]]) for i in range(len(y))]) for y in data2]
            legend = plt.legend([y[0][0][:10] for y in data2], loc='best')
            plt.xticks(range(25), list(range(20,24))+list(range(20)))
            plt.title('Request over same day of the week')
            plt.xlabel('Time of day - EST')

        # All days and all hours plots
        plt.rcParams.update({'font.size': 16})  # General font size

        f, axarr = plt.subplots(3, sharex=True)
        f.suptitle('Container: '+container, fontsize=30)

        # Total traffic plot        
        p = [(y[0],[sum(y[1][dev][0] for dev in y[1]),sum(y[1][dev][1] for dev in y[1]),sum(y[1][dev][2] for dev in y[1])]) for y in pStats]
        axarr[0].plot(range(len(p)),[x[1][1] for x in p], label='Total')
        axarr[1].plot(range(len(p)),[x[1][2] for x in p], label='Total')
        axarr[2].plot(range(len(p)),[x[1][2]/max(x[1][1],1) for x in p], label='Online performance')
        if do_by_day:
            online_perf = np.average([x[1][2]/max(x[1][1],1) for x in p])
        
        # Per-device traffic plot
        dev_types = {x for y in pStats for x in y[1].keys()}
        dev_types = dev_types - {'Other', 'N/A', 'Android', 'Tablet'}
        if len(dev_types) > 1:    
            for dev in dev_types:
                p = [(y[0],y[1][dev] if dev in y[1] else [0,0,0]) for y in pStats]
                axarr[0].plot(range(len(p)),[x[1][1] for x in p], label=dev)
                axarr[1].plot(range(len(p)),[x[1][2] for x in p], label=dev)
                axarr[2].plot(range(len(p)),[x[1][2]/max(x[1][1],1) for x in p], label=dev)
        
        # Baseline estimate
        if do_by_day:
            # IPS traffic plot
            p = [(y[0], y[1][0]/y[1][1]) for y in pStats_ips]
            axarr[2].plot(range(len(p)),[x[1] for x in p], label='Baseline estimate')
            baseline_est = np.average([x[1] for x in p])
            print('Online performance: {}'.format(online_perf))
            print('Baseline estimate: {}'.format(baseline_est))
            print('DS lift: {}'.format(online_perf/baseline_est))

        # Grid, axis and legend settings
        for ax in axarr:
            ax.grid(True)
        axarr[0].set(ylabel='Requests')
        axarr[1].set(ylabel='Rewards')
        axarr[2].set(ylabel='Ratio')
        plt.xticks([x[0] for x in days], [x[1] for x in days],rotation='vertical')
        legend = axarr[0].legend(loc='best')
        legend = axarr[1].legend(loc='best')
        legend = axarr[2].legend(loc='best')
        f.subplots_adjust(hspace=0.02, top=0.95, bottom=0.1)            
    
    plt.show()
