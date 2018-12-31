import requests, time, json, os, argparse, sys, collections, ds_parse, gzip
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

def send_rank_and_rewards(base_url, app, local_fp, feed=None, iter_num=1000, time_sleep=0.05, verbose=False):

    if not base_url.endswith('/'):
        base_url += '/'
    
    url = base_url+app
    rank_url = url+'/rank/'
    
    if feed:
        rank_url += feed
    else:
        context_list = ["""{"decisions":[{"shared":{"features":[{"user":{"name":"Doug"},"object":{"color":"brown","weigth":"light","temp":70}}]},"actions":[{"ids":[{"id":"bread"}]},{"ids":[{"id":"dog"}]},{"ids":[{"id":"box"}]},{"ids":[{"id":"envelope"}]}]}]}""", """{"decisions": [{"shared": {"features": [{"a": {"Gender": "female"},"b": {"Location": "New York"}}]},"actions": [{"ids": [{"id": "action3"}]},{"ids": [{"id": 1,"this creates":"'constant:1' feature for marginal"}],"this": {"doesn't get logged": "because is outside of 'features'"},"features": [{"missing namespace": "works anyway, since ns 'j' is added by rest API","float":{"duration":160.6},"int":{"parts":5}}]},{"ids": [{"id": 2}],"features": [{"ns": {"name": "action2"}}]}]}]}""","""{"decisions":[{"shared":{"features":[{"user":{"name":"Doug"},"object":{"color":"brown","weigth":"light","temp":70,"jack":[5,4,6],"jack2":{},"jack3":{"j":5,"g":4,"p":3}}}]},"actions":[{"ids":[{"id":"bread"}]},{"ids":[{"id":"dog"}]},{"ids":[{"id":"box"}]},{"ids":[{"id":"envelope"}]}]}]}"""]

    print('Sending rank/reward requests to url: {}'.format(rank_url))

    site_str = 'site://'+app
    s = requests.Session()
    s.headers.update({'Content-Type': 'application/json', 'Accept':'application/json'})
    
    os.makedirs(os.path.dirname(local_fp), exist_ok=True)
    with open(local_fp, 'a') as f:
        eventIds = []
        err = [0,0]
        for i in range(1,iter_num+1):
            if feed:
                r = s.get(rank_url)
            else:
                r = s.post(rank_url, context_list[i%len(context_list)])
            f.write('url:{}\tstatus_code:{}\theaders:{}\tcontent:{}\n'.format(rank_url,r.status_code,r.headers,r.content))
            if r.status_code != 200 or r.headers.get('x-msdecision-src', None) != site_str or b'rewardAction' not in r.content:
                err[0] += 1
                print('Rank Error - status_code: {}; headers: {}; content: {}'.format(r.status_code,r.headers,r.content))
            else:
                eventId = str(ds_parse.extract_field(r.content,b'eventId":"',b'","'), 'utf-8')
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

def print_stats(local_fp, azure_path, verbose=False, plot_hist=False, hist_bin=100):

    t = time.time()

    gt = {}
    len_local_rank = 0
    dup_rank = 0
    local_rew = []
    lines_errs = 0
    err_codes = collections.Counter()
    bytes_count = 0
    tot_bytes = os.path.getsize(local_fp)
    for i,x in enumerate(open(local_fp, encoding='utf-8')):
        bytes_count += len(x)
        if (i+1) % 10000 == 0:
            ds_parse.update_progress(bytes_count,tot_bytes,'Loading Local file: {} - '.format(local_fp))
        if 'status_code:200' in x:
            if '/rank/' in x and '"eventId":"' in x:
                ei = ds_parse.local_rank(x)
                len_local_rank += 1
                if ei in gt:
                    dup_rank += 1
                else:
                    gt[ei] = {'i': len_local_rank}
            elif '/reward/' in x and 'content:' in x:
                ei,r = ds_parse.local_reward(x)
                local_rew.append((ei,r))
                gt[ei].setdefault('local_rew',[]).append(r)
            else:
                lines_errs += 1
        else:
            err_codes.update([ds_parse.extract_field(x,'status_code:','\t')])
    ds_parse.update_progress(tot_bytes,tot_bytes,'Loading Local file: {} - '.format(local_fp))

    print('\n\nLoading Azure files...')
    if os.path.isdir(azure_path):
        files = [azure_fp.path for azure_fp in scantree(azure_path) if azure_fp.name.endswith('.json')]
    else:
        files = [azure_path]

    verbose_output = []

    ei_miss_local = 0
    azure_data = []
    for ii,azure_fp in enumerate(files):
        bytes_count = 0
        tot_bytes = os.path.getsize(azure_fp)
        for i,x in enumerate(gzip.open(azure_fp, 'rb') if azure_fp.endswith('.gz') else open(azure_fp, 'rb')):
            bytes_count += len(x)
            if (i+1) % 10000 == 0:
                if azure_fp.endswith('.gz'):
                    ds_parse.update_progress(i+1,prefix='File {}/{}: {} - '.format(ii+1,len(files),azure_fp))
                else:
                    ds_parse.update_progress(bytes_count,tot_bytes,'File {}/{}: {} - '.format(ii+1,len(files),azure_fp))

            if x.startswith(b'{"_label_cost":'):
                data = ds_parse.json_cooked(x)
                ei = str(data['ei'], 'utf-8')
                c = str(data['cost'], 'utf-8')
                azure_data.append((ei, c))
                if ei not in gt:
                    ei_miss_local += 1
                    if verbose:
                        verbose_output.append('Idx: {} - EventId: {} - Ranking missing from Local'.format(len(azure_data),ei))
                else:
                    gt[ei].setdefault('azure_data',[]).append((c, data['ts']))
        if azure_fp.endswith('.gz'):
            ds_parse.update_progress(i+1,prefix='File {}/{}: {} - '.format(ii+1,len(files),azure_fp))
        else:
            ds_parse.update_progress(bytes_count,tot_bytes,'File {}/{}: {} - '.format(ii+1,len(files),azure_fp))
        print()
    print()

    dup_azure_counter = collections.Counter()
    dup_rew_counter = collections.Counter()
    err_rewards_idx = []
    no_events_idx = []
    no_rewards_idx = []
    for i,ei in enumerate(gt):
        if (i+1) % 10000 == 0:
            ds_parse.update_progress(i+1,len(gt),'Evaluating differences - ')
        if 'local_rew' in gt[ei]:
            if len(gt[ei]['local_rew']) > 1:
                dup_rew_counter.update([len(gt[ei]['local_rew'])])
                if verbose:
                    verbose_output.append('Idx: {} - EventId: {} - Duplicate in Reward: {}'.format(gt[ei]['i'],ei,gt[ei]['local_rew']))
            else:
                if 'azure_data' in gt[ei]:
                    if len(gt[ei]['azure_data']) > 1:
                        dup_azure_counter.update([len(gt[ei]['azure_data'])])
                        if verbose:
                            verbose_output.append('Idx: {} - EventId: {} - Duplicate in Azure: {}'.format(gt[ei]['i'],ei,gt[ei]['azure_data']))
                    else:
                        a = float(gt[ei]['local_rew'][0])
                        b = float(gt[ei]['azure_data'][0][0])
                        if abs(a+b) > max(1e-7 * max(abs(a), abs(b)), 1e-6):
                            err_rewards_idx.append(gt[ei]['i'])
                            if verbose:
                                verbose_output.append('Idx: {} - EventId: {} - Error in reward: Local: {} Azure: {}'.format(gt[ei]['i'],ei,gt[ei]['local_rew'][0],gt[ei]['azure_data'][0]))
                else:
                    no_events_idx.append(gt[ei]['i'])
                    if verbose:
                        verbose_output.append('Idx: {} - EventId: {} - Ranking missing from Azure'.format(gt[ei]['i'],ei))
        else:
            no_rewards_idx.append(gt[ei]['i'])
            if verbose:
                verbose_output.append('Idx: {} - EventId: {} - Reward missing from local'.format(gt[ei]['i'],ei))
    ds_parse.update_progress(i+1,len(gt),'Evaluating differences - ')
    print()

    for x in verbose_output:
        print(x)

    print('\nComputing summary stats...')
    rew_dict = {y[0]: y[1] for y in local_rew}
    azure_dict = {y[0]: y[1] for y in azure_data}

    dup_azure = sum((x-1)*dup_azure_counter[x] for x in dup_azure_counter)
    dup_rew = sum((x-1)*dup_rew_counter[x] for x in dup_rew_counter)
    if verbose:
        print('-----'*10)
        print('Missing events indexes (1-based indexing)\n{}'.format(no_events_idx))
        print('-----'*10)
        print('Missing local rewards indexes (1-based indexing)\n{}'.format(no_rewards_idx))
        print('-----'*10)
        print('Wrong rewards indexes (1-based indexing)\n{}'.format(err_rewards_idx))
    print('-----'*10)
    print('Events in local_rank: {} (Duplicates: {})'.format(len_local_rank, dup_rank))
    print('Events in local_rew: {} (Duplicates: {} - {})'.format(len(local_rew), dup_rew, dup_rew_counter))
    print('Events in azure_data: {} (Duplicates: {} - {})'.format(len(azure_data), dup_azure, dup_azure_counter))
    print('-----'*10)
    print('Intersection local_rank/local_rew:',sum(1 for x in rew_dict if x in gt))
    print('Intersection local_rank/azure_data:',sum(1 for x in azure_dict if x in gt))
    print('Missing EventIds from local: {}'.format(ei_miss_local))
    print('Missing EventIds from azure: {}'.format(len(no_events_idx)), end='')
    if no_events_idx:
        print(' (oldest 1-base index: {}/{})'.format(min(no_events_idx),len_local_rank), end='')
    print('\nMissing Local Rewards: {}'.format(len(no_rewards_idx)), end='')
    if no_rewards_idx:
        print(' (oldest 1-base index: {}/{})'.format(min(no_rewards_idx),len_local_rank), end='')
    print('\nWrong rewards: {}'.format(len(err_rewards_idx)))
    print('-----'*10)
    print('status_codes errors: {}'.format(err_codes.most_common()))
    print('Lines skipped in Local file: {}'.format(lines_errs))
    print('-----'*10)
    print('Elapsed time: ',time.time()-t)
    if plot_hist:
        if err_rewards_idx or no_events_idx or no_rewards_idx:
            plt.rcParams.update({'font.size': 16})  # General font size
            if err_rewards_idx:
                a = plt.hist(err_rewards_idx, hist_bin, label='Wrong reward', color='xkcd:orange')
                if verbose:
                    print('err_rewards_idx',a)
            if no_events_idx:
                b = plt.hist(no_events_idx, hist_bin, label='No rank', color='xkcd:blue')
                if verbose:
                    print('no_events_idx',b)
            if no_rewards_idx:
                c = plt.hist(no_rewards_idx, hist_bin, label='No local reward', color='xkcd:red')
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
    parser.add_argument('-f','--feed', help="feed name", default=None)
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

        if kwargs['app']:
            kwargs.pop('azure_path')
            kwargs.pop('plot_hist')
            send_rank_and_rewards(**kwargs)
