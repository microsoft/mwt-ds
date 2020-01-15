import time,collections,os,types,gzip,sys
import simplejson

def update_progress(current, total=None, prefix=''):
    if total:
        barLength = 50 # Length of the progress bar
        progress = current/total
        block = int(barLength*progress)
        text = "\r{}Progress: [{}] {:.1f}%".format(prefix, "#"*block + "-"*(barLength-block), progress*100)
    else:
        text = "\r{}Iter: {}".format(prefix, current)
    sys.stdout.write(text)
    sys.stdout.flush()
    return len(text)-1      # return length of text string not counting the initial \r

#########################################################################  CREATE DSJSON FILES STATS #########################################################################

header_str = 'version,date,# obs,# rews,sum rews,# obs multi,# rews multi a,sum rews multi a,# obs1,# rews1,sum rews1,rews1 ips,tot ips slot1,tot slot1,rews rand ips,tot rand ips,tot unique,tot,not joined unique,not joined,not activated,corrupted,1,2,>2,max(a),time'

def process_files(files, output_file=None, d=None, e=None):
    t0 = time.time()
    fp_list = input_files_to_fp_list(files)
    if output_file:
        f = open(output_file, 'a', 1)
    print(header_str)
    for fp in fp_list:
        t1 = time.time()
        stats, d_s, e_s, d_c, e_c, slot_len_c, rew_multi_a, baselineRandom, not_activated, corrupted = process_dsjson_file(fp, d, e)
        res_list = os.path.basename(fp).replace('_0.json','').split('_data_',1)+[sum(stats[x][i] for x in stats) for i in ['o','Nr','r']]+rew_multi_a+([stats[1][i] for i in ['o','Nr','r','n','d','N']] if 1 in stats else [0,0,0,0,0,0])+baselineRandom+[len(d_s),d_c,len(e_s),e_c,not_activated,corrupted,slot_len_c[1],slot_len_c[2],sum(slot_len_c[i] for i in slot_len_c if i > 2),max(i for i in slot_len_c if slot_len_c[i] > 0),'{:.1f}'.format(time.time()-t1)]
        print(','.join(map(str,res_list)))
        if output_file:
            f.write('\t'.join(map(str,res_list))+'\n')
    if output_file:
        f.close()
    print('Total time: {:.1f} sec'.format(time.time()-t0))

def process_dsjson_file(fp, d=None, e=None):
    stats = {}
    slot_len_c = collections.Counter()
    e_s = set()
    d_s = set()
    e_c = 0
    d_c = 0
    not_activated = 0
    corrupted = 0
    rew_multi_a = [0,0,0]
    baselineRandom = [0,0]
    bytes_count = 0
    tot_bytes = os.path.getsize(fp)
    with (gzip.open(fp, 'rb') if fp.endswith('.gz') else open(fp, 'rb')) as file_input:
        for i,x in enumerate(file_input):
            bytes_count += len(x)
            if (i+1) % 1000 == 0:
                if fp.endswith('.gz'):
                    update_progress(i+1,prefix=fp+' - ')
                else:
                    update_progress(bytes_count,tot_bytes,fp+' - ')

            if x.startswith(b'['):   # Ignore checkpoint info line
                continue

            if not (x.startswith(b'{"') and x.strip().endswith(b'}')):
                corrupted += 1
                continue

            if x.startswith(b'{"_label_cost":'):
                data = json_cooked(x)
                if data is None:
                    corrupted += 1
                    continue

                if data['skipLearn']:    # Ignore not activated lines
                    not_activated += 1
                    continue

                slot_len_c.update([data['num_a']])
                if d is not None:
                    d.setdefault(data['ei'], []).append((data, fp, i))
                d_c += 1
                d_s.add(data['ei'])

                ############################### Aggregates for each file ########################################
                #
                # 'o':   number of events with a joined observation
                # 'Nr':  number of events with a non-zero reward
                # 'r':   sum of rewards
                # 'n':   IPS of numerator
                # 'd':   IPS of denominator (SNIPS = n/d)
                # 'N':   total number of events (IPS = n/N)
                #
                #################################################################################################

                if data['a'] not in stats:
                    stats[data['a']] = {'o':0,'Nr':0,'r':0.,'n':0.,'d':0.,'N':0}

                stats[data['a']]['N'] += 1
                stats[data['a']]['d'] += 1/data['p']
                baselineRandom[1] += 1/data['p']/data['num_a']
                if data['o'] == 1:
                    stats[data['a']]['o'] += 1
                    if data['num_a'] > 1:
                        rew_multi_a[0] += 1
                if data['cost'] != b'0':
                    r = -float(data['cost'])
                    stats[data['a']]['Nr'] += 1
                    stats[data['a']]['r'] += r
                    stats[data['a']]['n'] += r/data['p']
                    baselineRandom[0] += r/data['p']/data['num_a']
                    if data['num_a'] > 1:
                        rew_multi_a[1] += 1
                        rew_multi_a[2] += r
            else:
                data = json_dangling(x)

                if e is not None:
                    e.setdefault(data['ei'], []).append((data,fp,i))
                e_c += 1
                e_s.add(data['ei'])

        if fp.endswith('.gz'):
            len_text = update_progress(i+1,prefix=fp+' - ')
        else:
            len_text = update_progress(bytes_count,tot_bytes, fp+' - ')
        sys.stdout.write("\r" + " "*len_text + "\r")
        sys.stdout.flush()
    return stats, d_s, e_s, d_c, e_c, slot_len_c, rew_multi_a, baselineRandom, not_activated, corrupted

def input_files_to_fp_list(files):
    if not (isinstance(files, types.GeneratorType) or isinstance(files, list)):
        print('Input is not list or generator. Wrapping it into a list...')
        files = [files]
    fp_list = []
    for x in files:
        try:
            fp_list.append(x.path)
        except:
            fp_list.append(x)
    return fp_list

###############################################################################################################################################################################

def json_cooked(x, do_devType=False, do_VWState=False, do_p_vec=False):
    #################################
    # Optimized version based on expected structure:
    # {"_label_cost":0,"_label_probability":0.01818182,"_label_Action":9,"_labelIndex":8,"Timestamp":"2017-10-24T00:00:15.5160000Z","Version":"1","EventId":"fa68cd9a71764118a635fd3d7a908634","a":[9,11,3,1,6,4,10,5,7,8,2],"c":{"_synthetic":false,"User":{"_age":0},"Geo":{"country":"United States","_countrycf":"8","state":"New York","city":"Springfield Gardens","_citycf":"8","dma":"501"},"MRefer":{"referer":"http://www.complex.com/"},"OUserAgent":{"_ua":"Mozilla/5.0 (iPad; CPU OS 10_3_2 like Mac OS X) AppleWebKit/603.2.4 (KHTML, like Gecko) Version/10.0 Mobile/14F89 Safari/602.1","_DeviceBrand":"Apple","_DeviceFamily":"iPad","_DeviceIsSpider":false,"_DeviceModel":"iPad","_OSFamily":"iOS","_OSMajor":"10","_OSPatch":"2","DeviceType":"Tablet"},"_multi":[{"
    # {"_label_cost":0,"_label_probability":1,"_label_Action":1,"_labelIndex":0,"_deferred":true,"Timestamp":"2018-10-25T00:01:31.1780000Z","Version":"1","EventId":"28EF7EE0B9CF4E319696CB812973F0B3","DeferredAction":true,"a":[1],"c":{"Global":{"SLOT":"1"},"Request":{"DISPLOC":"BR"},"Profile":{"_REQASID":"F2A3670E24F94646983BEB3EB35CB0C9","COUNTRY":"BR","FPIGM":"FALSE","FPNIGM":"FALSE","FB":"FALSE"},"_multi":[{"Action":{"constant":1,"PayloadID":"425039838"}}]},"p":[1.000000],"VWState":{"m":"DF30E6A3648947E69EE6B0816BF42640/1A5CD300DFE546B7B29A11EB70980809"}}
    # {"_label_cost":0,"_label_probability":0.833333015,"_label_Action":6,"_labelIndex":5,"o":[{"EventId":"A3E5ADF82D3A4BD5A5161FFC19C95DBB","DeferredAction":false}],"Timestamp":"2018-10-25T00:00:00.3960000Z","Version":"1","EventId":"A3E5ADF82D3A4BD5A5161FFC19C95DBB","DeferredAction":true,"a":[6,2,4,5,1,3],"c":{"Global":{"SLOT":"2"},"Request":{"DISPLOC":"US"},"Profile":{"_REQASID":"70A03B1FD0CA4B5A89669637DD448161","COUNTRY":"US","FPIGM":"FALSE","FPNIGM":"FALSE","FB":"FALSE","F1":0.0,"F2":0.0,"F3":0.00157480315,"F4":0.00182481752,"F5":0.0,"F6":0.00136892539,"F7":0.0,"F8":-1.0,"F9":0.0434782609,"F11":0.0,"F12":0.0,"F13":0.0,"F14":1.0,"F15":1.0,"F16":0.0,"F17":2.0,"F18":0.0,"F19":0.0,"F20":1.0
    # Assumption: "Version" value is 1 digit string
    #
    # Performance: 4x faster than Python JSON parser js = json.loads(x.strip())
    #################################
    ind1 = x.find(b',',16)              # equal to: x.find(',"_label_prob',16)
    ind2 = x.find(b',',ind1+23)         # equal to: x.find(',"_label_Action',ind1+23)
    ind3 = x.find(b',"T',ind2+34)       # equal to: x.find(',"Timestamp',ind2+34)
    ind4 = x.find(b'"',ind3+33)
    if x[ind4+3:ind4+10] == b'Version': # check for the presence of "Version":"1"
        ind5 = ind4+27                  # len('","Version":"1","EventId":"') = 27
    else:
        ind5 = ind4+13                  # len('","EventId":"') = 13
    ind6 = x.find(b'"',ind5)
    ind7 = x.find(b',"a"',ind5)
    ind8 = x.find(b'],"c"',ind7+7)
    if ind8 == -1:
        return None

    data = {}
    data['o'] = 1 if b',"o":' in x[ind2+30:ind2+50] else 0
    data['cost'] = x[15:ind1]                   # len('{"_label_cost":') = 15
    data['p'] = float(x[ind1+22:ind2])          # len(',"_label_probability":') = 22
    data['ts'] = x[ind3+14:ind4]                # len(',"Timestamp":"') = 14
    data['ei'] = x[ind5:ind6]
    data['a_vec'] = x[ind7+6:ind8].split(b',')  # len(',"a":[') = 6
    data['a'] = int(data['a_vec'][0])
    data['num_a'] = len(data['a_vec'])
    data['skipLearn'] = b'"_skipLearn":true' in x[ind2+34:ind3] # len('"_label_Action":1,"_labelIndex":0,') = 34

    if data['p'] < 1e-10 or data['a'] < 1 or data['num_a'] < 1:
        return None

    if do_VWState:
        ind11 = x[-120:].find(b'VWState')
        data['model_id'] = x[-120+ind11+15:-4] if ind11 > -1 else b'N/A'

    if do_p_vec:
        data['p_vec'] = [float(p) for p in extract_field(x[-120-15*data['num_a']:], b'"p":[', b']').split(b',')]

    if do_devType:
        data['devType'] = extract_field(x[ind8:],b'"DeviceType":"',b'"')

    return data


def ccb_json_cooked(x):
    try:
        data = simplejson.loads(x)
    except Exception:
        print(x)

    dashboard_data = {}
    dashboard_data['ts'] = data['Timestamp']
    dashboard_data['_outcomes'] = data['_outcomes']
    return dashboard_data

def json_dangling(x):
    #################################
    # Optimized version based on expected structure:
    # {"Timestamp":"2017-11-27T01:19:13.4610000Z","RewardValue":1.0,"EnqueuedTimeUtc":"2017-08-23T03:31:06.85Z","EventId":"d8a0391be9244d6cb124115ba33251f6"}
    # {"RewardValue":1.0,"EnqueuedTimeUtc":"2018-01-03T20:12:20.028Z","EventId":"tr-tr_8580.Hero.HyxjxHF8/0WMGsuP","Observations":[{"v":1.0,"EventId":"tr-tr_8580.Hero.HyxjxHF8/0WMGsuP","ActionId":null}]}
    # {"RewardValue":1.0,"DeferredAction":false,"EnqueuedTimeUtc":"2018-10-26T01:23:00.825Z","EventId":"6F61036134274192BE3537D3E4E84ECF","Observations":[{"v":null,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":false},{"v":null,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":false},{"v":null,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":false},{"v":null,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":false},{"v":null,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":false},{"v":1.0,"EventId":"6F61036134274192BE3537D3E4E84ECF","ActionId":null,"DeferredAction":true}]}
    #
    # Performance: 3x faster than Python JSON parser js = json.loads(x.strip())
    #################################
    data = {}
    if x.startswith(b'{"Timestamp"'):
        ind1 = x.find(b'"',36)              # equal to: x.find('","RewardValue',36)
        ind2 = x.find(b',',ind1+16)         # equal to: x.find(',"EnqueuedTimeUtc',ind1+16)
        ind3 = x.find(b'"',ind2+39)             # equal to: x.find('","EventId',ind2+39)
        ind4 = x.find(b'"',ind3+40)

        data['r'] = x[ind1+16:ind2]         # len('","RewardValue":') = 16
        data['et'] = x[ind2+20:ind3]                    # len(',"EnqueuedTimeUtc":"') = 20
        data['ei'] = x[ind3+13:ind4]                    # len('","EventId":"') = 13
    else:
        ind2 = x.find(b',',15)
        ind3 = x.find(b',"Enq',ind2)
        ind4 = x.find(b'"',ind3+39)           # equal to: x.find('","EventId',ind2+39)
        ind5 = x.find(b'"',ind4+40)

        data['r'] = x[15:ind2]                # len('{"RewardValue":') = 15
        data['et'] = x[ind3+19:ind4]                    # len(',"EnqueuedTimeUtc":"') = 20
        data['ei'] = x[ind4+13:ind5]                    # len('","EventId":"') = 13

    data['ActionTaken'] = b'"DeferredAction":false' in x[:70]
    return data

def extract_field(x,sep1,sep2,space=1):
    ind1 = x.find(sep1)
    if ind1 < 0:
        return b'N/A'
    ind1 += len(sep1)
    ind2 = x.find(sep2,ind1+space)
    if ind2 == -1:
        return x[ind1:]
    else:
        return x[ind1:ind2]

def local_rank(x):
    #################################
    # Optimized version based on expected structure:
    # url:https://ds-staging.microsoft.com/api/v2/marco-test-338/rank/complex_videos	status_code:200	headers:[...],"eventId":"ee9e857b57644a3fa600bc0343952ae8-sVvR","appId":"marco-test-338",[...]\n'
    #################################
    ind1 = x.find('"eventId":"')
    ind2 = x.find('"',ind1+45)
    return x[ind1+11:ind2]

def local_reward(x):
    #################################
    # Optimized version based on expected structure:
    # url:https://ds-staging.microsoft.com/api/v2/marco-test-338/reward/ee9e857b57644a3fa600bc0343952ae8-sVvR	status_code:200	headers:[...]	content:5.36
    #################################
    ind1 = x.find('/reward/')
    ind2 = x.find('\t',ind1+42)
    ind3 = x.find('\tcontent:',len(x)-35)
    return x[ind1+8:ind2],x.strip()[ind3+9:]

def cmplx_json_to_csv(input_file, output_file):
    # Used to parse cmplx dsjson lines to csv file
    with open(output_file, 'w', encoding='utf-8') as f, open(input_file, encoding='utf-8') as fin:
        f.write('cost,prob,city,country,state,DeviceBrand,DeviceFamily,DeviceModel,DeviceType,refer,id\n')
        i = 0
        for x in fin:
            try:
                js = json.loads(x.strip())
                d2 = '"'+('","'.join((js['c']['OUserAgent'].get(x, 'NA').replace('"','') for x in ['_DeviceBrand', '_DeviceFamily', '_DeviceModel', 'DeviceType'])))+'"'
                d2 = d2.replace('"NA"','NA')
                if 'Geo' in js['c']:
                    d1 = '"'+('","'.join([js['c']['Geo'].get(x, 'NA') for x in ['city', 'country', 'state']]))+'"'
                    d1 = d1.replace('"NA"','NA')
                else:
                    d1 = 'NA,NA,NA'
                if 'MRefer' in js['c']:
                    d3 = '"'+js['c']['MRefer']['referer']+'"'
                else:
                    d3 = 'NA'
                d4 = '"'+js['c']['_multi'][js['_labelIndex']]['i']['id']+'"'
            except:
                print('error',x)
                continue
            f.write(str(js['_label_cost'])+','+str(js['_label_probability'])+','+','.join([d1,d2,d3,d4])+'\n')
            i += 1
            if i % 100000 == 0:
                print(i)

def get_e_from_eh_obs(fp):
    #################################
    # Optimized version based on expected structure:
    # Time:4/27/2018 11:06:31 AM Offset:4305049088 Partition:-11 Seq:256976 Size:126 Data:{"EventId":"WW_Home_Slot_2_20170601_S2_466x264.03B85FA05B4C4704BC5962FB9A29FA36","v":1.0}
    #################################
    e = {};
    with open(fp, 'rb') as f:
        for x in f:
            ind1 = x.find(b' Offset:')
            ind2 = x.find(b'Partition:', ind1+8)
            ind3 = x.find(b' ', ind2+11)
            ind4 = x.find(b'"EventId":"', ind3+1)
            ind5 = x.find(b'","', ind4+11)

            data = {}
            data['ts'] = x[5:ind1]
            data['partition'] = x[ind2+10:ind3]
            ei = x[ind4+11:ind5]
            data['value'] = x[ind5+6:].strip()[:-1]
            e.setdefault(ei, []).append((data,))
    return e

def create_time_hist(d,e, normed=True, cumulative=True, scale_sec=1, n_bins=100, td_day_start=None, ei=None, xlabel=None, ylabel=None):
    import matplotlib.pyplot as plt
    import datetime
    t_vec = []
    ei_ = {x for x in e if x in d}
    print('len(e): {}'.format(len(e)))
    print('len(d): {}'.format(len(d)))
    print('len(e inter d): {}'.format(len(ei_)))
    if ei is not None:
        ei_ = ei_.intersection(ei)
        print('len(ei): {}'.format(len(ei)))
        print('len(ei_): {}'.format(len(ei_)))

    for x in ei_:
        td = datetime.datetime.strptime(str(d[x][0][0]['ts'],'utf-8').split('.')[0].replace('Z',''), "%Y-%m-%dT%H:%M:%S")
        if td_day_start and td < datetime.datetime.strptime(td_day_start, "%Y-%m-%d"):
            continue
        ts = e[x][0][0]['ts']
        if b' ' in ts:
            te = datetime.datetime.strptime(str(ts,'utf-8'), '%m/%d/%Y %I:%M:%S %p')
        else:
            te = datetime.datetime.strptime(str(ts,'utf-8').split('.', 1)[0].replace('Z',''), "%Y-%m-%dT%H:%M:%S")
        t_vec.append((x,(te-td).total_seconds()/scale_sec))

    print('len(t_vec): {}'.format(len(t_vec)))
    plt.hist([x[1] for x in t_vec], n_bins, normed=normed, cumulative=cumulative, histtype='step')
    if xlabel:
        plt.xlabel(xlabel)
    if ylabel:
        plt.ylabel(ylabel)
    plt.show()

    return t_vec


'''
####################################################################################################################################
# SLOW IMPLEMENTATIONS ALREADY TESTED
####################################################################################################################################

import ijson, io
def ijson_cooked(x):
    ##############################################
    # 35x slower than json_cooked with the pure Python parser
    # I'm unable to install YAJL, to use import ijson.backends.yajl2_cffi as ijson
    ##############################################
    f = io.StringIO(x.strip())
    parser = ijson.parse(f)
    i = 0
    output = []
    for prefix, event, value in parser:
        i += 1
        if prefix != None and event != 'map_key':
            output.append(value)
        if prefix == 'c':
            break
    return output[7],output[1],output[5],float(output[2]),output[3],len(output)-11

import parse
def parse_cooked(x):
    ##############################################
    # 40x slower than json_cooked with the pure Python parser
    # I'm unable to install YAJL, to use import ijson.backends.yajl2_cffi as ijson
    ##############################################
    r = parse.parse('{"_label_cost":{:n},"_label_probability":{:g},"_label_Action":{:n},"_labelIndex":{:n},"Timestamp":"{}","Version":"{:n}","EventId":"{}","a":{},{}', x)
    return r[7],r[1],r[5],r[2],r[3],len(r.fixed)-8

'''
