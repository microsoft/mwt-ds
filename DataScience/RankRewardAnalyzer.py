import requests, time, json, os, argparse, sys


def update_progress(current, total, display=''):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    text = "\rProgress: [{}] {:.1f}% - Iter: {}/{} - {}".format( "#"*block + "-"*(barLength-block), progress*100,current,total,display)
    sys.stdout.write(text)
    sys.stdout.flush()

def send_rank_and_rewards(base_url, app, local_fp, feed, iter_num=1000, time_sleep=0.05):

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
            f.write('url:{}\tstatus_code:{}\theaders:{}\tcontent:{}\n'.format(rank_url,r.status_code,r.headers['x-msdecision-src'],r.content))
            if r.status_code != 200 or r.headers['x-msdecision-src'] != site_str or b'rewardAction' not in r.content:
                err[0] += 1
                print('Rank Error - status_code: {}; headers: {}; content: {}',r.status_code,r.headers['x-msdecision-src'],r.content)
                continue
            eventId = str(r.content.split(b'eventId":"',1)[1].split(b'","',1)[0], 'utf-8')
            eventIds.append(eventId)
            r2 = s.post(url+'/reward/'+eventId, json=i+.18)
            f.write('url:{}\tstatus_code:{}\theaders:{}\tcontent:{}\n'.format(url+'/reward/'+eventId,r2.status_code,r2.headers['x-msdecision-src'],i+.18))
            if r2.status_code != 200 or r2.headers['X-MSDecision-Src'] != site_str:
                err[1] += 1
                print('Reward Error - status_code: {}; headers: {}',r2.status_code,r2.headers['x-msdecision-src'])
            
            update_progress(i, iter_num, 'Errors: {}'.format(err))
            time.sleep(time_sleep)
            
    return eventIds

def print_stats(local_fp, azure_path):

    print('Computing statistics...')

    local_rank = [x.strip().split('"eventId":"',1)[1].split('","',1)[0] for x in open(local_fp, encoding='utf-8') if '/rank/' in x]
    local_rew = [(x.strip().split('/reward/',1)[1].split('\t',1)[0],x.strip().split('content:',1)[1]) for x in open(local_fp, encoding='utf-8') if '/reward/' in x]
    if os.path.isdir(azure_path):
        azure_data = [(x.strip().split('EventId":"',1)[1].split('","',1)[0], x.strip().split('_label_cost":',1)[1].split(',"',1)[0]) for azure_fp in os.scandir(azure_path) if azure_fp.name.endswith('.json') for x in open(azure_fp.path, encoding='utf-8')]
    else:
        azure_data = [(x.strip().split('EventId":"',1)[1].split('","',1)[0], x.strip().split('_label_cost":',1)[1].split(',"',1)[0]) for x in open(azure_path, encoding='utf-8')]
    
    rew_dict = {y[0] : y[1] for y in local_rew}
    azure_dict = {y[0] : y[1] for y in azure_data}
    
    err_rewards_idx = []
    no_events_idx = []
    for i,x in enumerate(local_rank):
        if x in azure_dict:
            if float(rew_dict[x]) != -float(azure_dict[x]):
                print('Idx: {} - Error in reward: Local: {} Azure: {} - EventId: {}'.format(i+1,rew_dict[x], azure_dict[x],x))
                err_rewards_idx.append(i+1)
        else:
            no_events_idx.append(i+1)
            print('Idx: {} - Ranking missing from Azure - EventId: {}'.format(i+1,x))
    print('-----'*10)
    print('Missing events indexes (1-based indexing)\n{}'.format(no_events_idx))
    print('-----'*10)
    print('Wrong rewards indexes (1-based indexing)\n{}'.format(err_rewards_idx))
    print('-----'*10)
    print('Events in local_rew: {} (Duplicates: {})'.format(len(local_rew), len(local_rew)-len(rew_dict)))
    print('Events in local_rank: {} (Duplicates: {})'.format(len(local_rank), len(local_rank)-len(set(local_rank))))
    print('Events in azure_data: {} (Duplicates: {})'.format(len(azure_data), len(azure_data)-len(azure_dict)))
    print('-----'*10)
    print('Intersection local_rew/local_rank:',len(set(rew_dict.keys()).intersection(local_rank)))   
    print('Intersection local_rew/azure_data:',len(set(rew_dict.keys()).intersection(azure_dict.keys())))
    print('Missing EventIds: {} (oldest index: {}/{})\nWrong rewards: {}'.format(len(no_events_idx),min(no_events_idx),len(local_rank),len(err_rewards_idx)))
    print('-----'*10)


if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-l','--local_fp', help="file path for writing/reading sent events", required=True)
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--azure_path', help="file or directory with azure storage data in .json format (when flag is present, statistics with --local_fp will be computed")
    group.add_argument('-a','--app', help="app name")
    parser.add_argument('-f','--feed', help="feed name")
    parser.add_argument('-u','--base_url', default="https://ds.microsoft.com/api/v2/", help="base url (default: https://ds.microsoft.com/api/v2/)")
    parser.add_argument('-i','--iter_num', type=int, default=1000, help="number of requests/rewards (default: 1000)")
    parser.add_argument('-t','--time_sleep', default=0.05, help="time_sleep between interations (default: 0.05)")
    
    
    kwargs = vars(parser.parse_args())
    
    if kwargs['azure_path']:
        print_stats(kwargs['local_fp'], kwargs['azure_path'])
    else:
        if not kwargs['app']:
            print('When sending rank/reward requests, --app is required')
        
        if not kwargs['feed']:
            print('When sending rank/reward requests, --feed is required')

        if kwargs['feed'] and kwargs['app']:
            kwargs.pop('azure_path')
            send_rank_and_rewards(**kwargs)
