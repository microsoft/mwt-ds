import json, os, time, pickle, sys
import matplotlib.pyplot as plt
import numpy as np
import configparser
import LogDownloader
import ds_parse


# Create dictionary with filename as keys
def parse_logs(raw_stats, files, delta_mod_t=3600):
    t0 = time.time()
    
    for fp in files:
        delta_t = time.time()-os.path.getmtime(fp)
        if os.path.basename(fp) in raw_stats:
            if delta_t > delta_mod_t:
                continue
            print('Processing: {} - Last modified: {:.1f} sec ago < delta_mod_t={} sec'.format(fp,delta_t,delta_mod_t))
        else:
            print('Processing: {}'.format(fp))
        
        c2 = {}
        ii = 0
        for line in open(fp, 'rb'):
            ii += 1
            if ii % 10000 == 0:
                print(ii)
            if not line.startswith(b'{"_label_cost'):
                continue
            
            ei,r,ts,p,a,num_a,dev = ds_parse.json_cooked(line, do_devType=True)
            
            # extract date from ts
            d = str(ts[:13], 'utf-8')
            dev = str(dev, 'utf-8')
            
            if d not in c2:
                c2[d] = {}
            if dev not in c2[d]:
                c2[d][dev] = [0,0,0]
            if 'ips' not in c2:
                c2['ips'] = {}
            if d[:10] not in c2['ips']:
                c2['ips'][d[:10]] = [0,0,0,0]
                
            c2[d][dev][1] += 1
            if a == 1:
                c2['ips'][d[:10]][1] += 1/p
            c2['ips'][d[:10]][3] += 1/p/num_a
            if r != b'0':
                r = float(r)
                c2[d][dev][0] += 1
                c2[d][dev][2] -= r
                if a == 1:
                    c2['ips'][d[:10]][0] -= r/p
                c2['ips'][d[:10]][2] -= r/p/num_a

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
    kwargs = LogDownloader.parse_argv(sys.argv)
    app_id = kwargs['app_id']
    log_dir = kwargs['log_dir']
    delta_mod_t = kwargs['delta_mod_t']
    
    ################################# DATA DOWNLOADER #########################################################
    
    if len(sys.argv) > 5:
        LogDownloader.download_container(**kwargs)
        
    ################################# PARSE LOGS #########################################################

    raw_stats = {}
    pkl_fp = os.path.join(log_dir, 'ds_'+app_id+'_hours.pickle')
    if os.path.isfile(pkl_fp):
        with open(pkl_fp, 'rb') as pkl_file:
            raw_stats = pickle.load(pkl_file)

    print('raw_stats.keys():',raw_stats.keys())

    files = [x.path for x in os.scandir(os.path.join(log_dir,app_id)) if x.path.endswith('.json') and '_skip' not in x.name and x.name.startswith('20') and x.name[14:20] == '_data_']

    parse_logs(raw_stats, files, delta_mod_t)
    
    # Update picke file
    with open(pkl_fp, 'wb') as pkl_file:
        pickle.dump(raw_stats, pkl_file)

    # Create dictionary with hours as keys
    stats = {}
    stats_ips = {}
    for fn in raw_stats:
        for d in raw_stats[fn]:
            if d == 'ips':
                for day in raw_stats[fn]['ips']:
                    # if there are multiple entries with same hour, take the one with the largest total traffic
                    if day not in stats_ips or raw_stats[fn]['ips'][day][-1] > stats_ips[day][-1]:
                        stats_ips[day] = raw_stats[fn]['ips'][day]
            else:
                # if there are multiple entries with same hour, take the one with the largest total traffic (sum over all devices)
                if d not in stats or sum(raw_stats[fn][d][dev][1] for dev in raw_stats[fn][d]) > sum(stats[d][dev][1] for dev in stats[d]):
                    stats[d] = raw_stats[fn][d]
    
    pStats = sorted([(d,stats[d]) for d in stats])
    pStats_ips = sorted([(day,stats_ips[day]) for day in stats_ips])
    
    ############################ VISUALIZATIONS ##################################################
    for do_by_day in [False, True]:    
        if do_by_day:
            pStats = convert_pStats_from_hours_to_days(pStats)      # update pStats to be day by day
            days = [(i,x[0]) for i,x in enumerate(pStats)]
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
        f.suptitle('App Id: '+app_id, fontsize=30)

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
                p = [(y[0],y[1].get(dev, [0,0,0])) for y in pStats]
                axarr[0].plot(range(len(p)),[x[1][1] for x in p], label=dev)
                axarr[1].plot(range(len(p)),[x[1][2] for x in p], label=dev)
                axarr[2].plot(range(len(p)),[x[1][2]/max(x[1][1],1) for x in p], label=dev)
        
        # Baseline estimate
        if do_by_day:
            # IPS traffic plot
            p = [(y[0], y[1][0]/y[1][1]) for y in pStats_ips]
            axarr[2].plot(range(len(p)),[x[1] for x in p], label='Baseline1')
            baseline1_est = np.average([x[1] for x in p])
            p = [(y[0], y[1][2]/y[1][3]) for y in pStats_ips]
            axarr[2].plot(range(len(p)),[x[1] for x in p], label='BaselineRand')
            baselineR_est = np.average([x[1] for x in p])
            print('Online performance: {:.4f}'.format(online_perf))
            print('Baseline1 estimate: {:.4f}'.format(baseline1_est))
            print('BaselineR estimate: {:.4f}'.format(baselineR_est))
            print('DS lift: R:{:.4f} 1:{:.4f}'.format(online_perf/baselineR_est, online_perf/baseline1_est))

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
