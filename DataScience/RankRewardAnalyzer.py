import requests, time, json, os, argparse, sys, collections
import matplotlib.pyplot as plt


def scantree(path):
    """Recursively yield DirEntry objects for given directory."""
    for entry in os.scandir(path):
        if entry.is_dir(follow_symlinks=False):
            yield from scantree(entry.path)
        else:
            yield entry

def update_progress(current, total, display=''):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    text = "\rProgress: [{}] {:.1f}% - Iter: {}/{} - {}".format( "#"*block + "-"*(barLength-block), progress*100,current,total,display)
    sys.stdout.write(text)
    sys.stdout.flush()
    
def dup_analysis(log_list):
    c = collections.Counter(y[0] for y in log_list)
    data = {}
    for i,x in enumerate(log_list):
        if c[x[0]] > 1:
            data.setdefault(x[0], []).append((i,x[1]))
    for x in data:
        print(x, data[x])

def send_rank_and_rewards(base_url, app, local_fp, feed, iter_num=1000, time_sleep=0.05, verbose=False):

    print('Sending rank/reward requests...')

    if not base_url.endswith('/'):
        base_url += '/'
    
    url = base_url+app
    rank_url = url+'/rank/'+feed
    
    site_str = 'site://'+app
    s = requests.Session()
    s.headers.update({'Content-Type': 'application/json', 'Accept':'application/json'})
    
    with open(local_fp, 'a') as f:
        eventIds = []
        err = [0,0]
        for i in range(1,iter_num+1):
            r = s.get(rank_url)
            f.write('url:{}\tstatus_code:{}\theaders:{}\tcontent:{}\n'.format(rank_url,r.status_code,r.headers,r.content))
            if r.status_code != 200 or r.headers.get('x-msdecision-src', None) != site_str or b'rewardAction' not in r.content:
                err[0] += 1
                print('Rank Error - status_code: {}; headers: {}; content: {}'.format(r.status_code,r.headers,r.content))
            else:
                eventId = str(r.content.split(b'eventId":"',1)[1].split(b'","',1)[0], 'utf-8')
                eventIds.append(eventId)
                reward = i+.36
                r2 = s.post(url+'/reward/'+eventId, json=reward)
                f.write('url:{}\tstatus_code:{}\theaders:{}\tcontent:{}\n'.format(url+'/reward/'+eventId,r2.status_code,r2.headers,reward))
                if r2.status_code != 200 or r2.headers.get('x-msdecision-src', None) != site_str:
                    err[1] += 1
                    print('Reward Error - status_code: {}; headers: {}'.format(r2.status_code,r2.headers))
            
            update_progress(i, iter_num, 'Errors: {}'.format(err))
            time.sleep(time_sleep)
            
    return eventIds

def print_stats(local_fp, azure_path, verbose=False, plot_hist=False):

    print('Computing statistics...')

    local_rank = []
    local_rew = []
    lines_errs = 0
    err_codes = collections.Counter()
    for x in open(local_fp, encoding='utf-8'):
        if 'status_code:200' in x:
            if '/rank/' in x and '"eventId":"' in x:
                local_rank.append(x.split('"eventId":"',1)[1].split('","',1)[0])
            elif '/reward/' in x and 'content:' in x:
                local_rew.append((x.split('/reward/',1)[1].split('\t',1)[0], x.strip().split('content:',1)[1]))
            else:
                lines_errs += 1
        else:
            err_codes.update([x.split('status_code:',1)[1].split('\t',1)[0]])

    if os.path.isdir(azure_path):
        files = [azure_fp.path for azure_fp in scantree(azure_path) if azure_fp.name.endswith('.json')]
    else:
        files = [azure_path]
    azure_data = [(x.strip().split('EventId":"',1)[1].split('","',1)[0], x.strip().split('_label_cost":',1)[1].split(',"',1)[0]) for azure_fp in files for x in open(azure_fp, encoding='utf-8') if x.startswith('{"_label_cost":')]
    
    local_rank_set = set(local_rank)
    rew_dict = {y[0] : y[1] for y in local_rew}
    azure_dict = {y[0] : y[1] for y in azure_data}
    
    err_rewards_idx = []
    no_events_idx = []
    no_rewards_idx = []
    for i,x in enumerate(local_rank):
        if x in rew_dict:
            if x in azure_dict:
                if abs(1. + float(azure_dict[x])/float(rew_dict[x])) > 1e-7:
                    if verbose:
                        print('Idx: {} - Error in reward: Local: {} Azure: {} - EventId: {}'.format(i+1,rew_dict[x], azure_dict[x],x))
                    err_rewards_idx.append(i+1)
            else:
                no_events_idx.append(i+1)
                if verbose:
                    print('Idx: {} - Ranking missing from Azure - EventId: {}'.format(i+1,x))
        else:
            no_rewards_idx.append(i+1)
            if verbose:
                print('Idx: {} - Reward missing from local - EventId: {}'.format(i+1,x))

    dup_local = len(local_rew)-len(rew_dict)
    dup_azure = len(azure_data)-len(azure_dict)
    if verbose:
        print('-----'*10)
        print('Missing events indexes (1-based indexing)\n{}'.format(no_events_idx))
        print('-----'*10)
        print('Missing local rewards indexes (1-based indexing)\n{}'.format(no_rewards_idx))
        print('-----'*10)
        print('Wrong rewards indexes (1-based indexing)\n{}'.format(err_rewards_idx))
        if dup_local > 0:
            print('-----'*10)
            print('Duplicates in Local rewards')
            dup_analysis(local_rew)    
        if dup_azure > 0:
            print('-----'*10)
            print('Duplicates in Azure Storage')
            dup_analysis(azure_data)
    print('-----'*10)
    print('Events in local_rank: {} (Duplicates: {})'.format(len(local_rank), len(local_rank)-len(local_rank_set)))
    print('Events in local_rew: {} (Duplicates: {})'.format(len(local_rew), dup_local))
    print('Events in azure_data: {} (Duplicates: {})'.format(len(azure_data), dup_azure))
    print('-----'*10)
    print('Intersection local_rank/local_rew:',len(local_rank_set.intersection(rew_dict.keys())))
    print('Intersection local_rank/azure_data:',len(local_rank_set.intersection(azure_dict.keys())))
    print('Missing EventIds: {}'.format(len(no_events_idx)), end='')
    if no_events_idx:
        print(' (oldest 1-base index: {}/{})'.format(min(no_events_idx),len(local_rank)), end='')
    print('\nMissing Local Rewards: {}'.format(len(no_rewards_idx)), end='')
    if no_rewards_idx:
        print(' (oldest 1-base index: {}/{})'.format(min(no_rewards_idx),len(local_rank)), end='')
    print('\nWrong rewards: {}'.format(len(err_rewards_idx)))
    print('-----'*10)
    print('status_codes errors: {}'.format(err_codes.most_common()))
    print('Lines skipped in Local file: {}'.format(lines_errs))
    print('-----'*10)
    if plot_hist:
        if err_rewards_idx or no_events_idx or no_rewards_idx:
            plt.rcParams.update({'font.size': 16})  # General font size
            if err_rewards_idx:
                a = plt.hist(err_rewards_idx, 50, label='Wrong reward', color='xkcd:orange')
                if verbose:
                    print('err_rewards_idx',a)
            if no_events_idx:
                b = plt.hist(no_events_idx, 50, label='No rank', color='xkcd:blue')
                if verbose:
                    print('no_events_idx',b)
            if no_rewards_idx:
                c = plt.hist(no_rewards_idx, 50, label='No local reward', color='xkcd:red')
                if verbose:
                    print('no_rewards_idx',c)
            plt.title('Missing/Wrong rank and reward requests', fontsize=20)
            plt.xlabel('Request index', fontsize=18)
            plt.ylabel('Bin Count', fontsize=18)
            plt.legend()
            plt.show()
        else:
            print('Nothing to plot! All is good!')


if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-l','--local_fp', help="file path for writing/reading sent events", required=True)
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--azure_path', help="file or directory with azure storage data in .json format (when flag is present, statistics with --local_fp will be computed")
    group.add_argument('-a','--app', help="app name")
    parser.add_argument('-f','--feed', help="feed name")
    parser.add_argument('-u','--base_url', default="https://ds.microsoft.com/api/v2/", help="base url (default: https://ds.microsoft.com/api/v2/)")
    parser.add_argument('-i','--iter_num', type=int, default=1000, help="number of requests/rewards (default: 1000)")
    parser.add_argument('-t','--time_sleep', type=float, default=0.05, help="time_sleep between interations (default: 0.05)")
    parser.add_argument('-v','--verbose', help="print eventId of missing rank and rewards", action='store_true')
    parser.add_argument('-p','--plot_hist', help="plot hist of missing rank and rewards", action='store_true')
    
    
    kwargs = vars(parser.parse_args())
    
    if kwargs['azure_path']:
        print_stats(kwargs['local_fp'], kwargs['azure_path'], kwargs['verbose'], kwargs['plot_hist'])
    else:
        if not kwargs['app']:
            print('When sending rank/reward requests, --app is required')
        
        if not kwargs['feed']:
            print('When sending rank/reward requests, --feed is required')

        if kwargs['feed'] and kwargs['app']:
            kwargs.pop('azure_path')
            kwargs.pop('plot_hist')
            send_rank_and_rewards(**kwargs)
