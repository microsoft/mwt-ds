from datetime import datetime
from loggers import Logger
import re

class CookedLogSequence:
    def __init__(self, files):
        self.files = files

    def get_file_start_time(self, fp):
        with open(fp, 'r') as f:
            first_line = f.readline()
            return datetime.strptime(re.search('Timestamp":"(.*?)0Z', first_line).group(1), "%Y-%m-%dT%H:%M:%S.%f")
            f.close()
    
    def get_file_end_time(self, fp):
        with open(fp, 'r') as f:
            last_line = f.readlines()[-1]
            return datetime.strptime(re.search('Timestamp":"(.*?)0Z', last_line).group(1), "%Y-%m-%dT%H:%M:%S.%f")
            f.close()
    
    def get_first_ts(self):
        if (len(self.files) > 0):
            return self.get_file_start_time(self.files[0])
        return datetime.strptime('2000-01-01T00:00:00.000000', "%Y-%m-%dT%H:%M:%S.%f")
    
    def get_last_ts(self):
        if (len(self.files) > 0):
            return self.get_file_end_time(self.files[-1])
        return datetime.strptime('2000-01-01T00:00:00.000000', "%Y-%m-%dT%H:%M:%S.%f")

    #Merging of CookedLogSequence must be in sequential order
    #Ex. We have three sequences in the following order A B C
    #if we merge(A,C), B cannot later be merged
    #we have to in sequential order merge(merge(A,B),C). 
    def merge(self, other):
        merged_files = self.files
        merged_end_time = self.get_last_ts()
        merge_remaining_files = other.get_first_ts() > merged_end_time
        for fp in other.files:
            if merge_remaining_files:
                merged_files.append(fp)
            else:
                file_end_time = other.get_file_end_time(fp)
                output_file_name = fp[0:-5] + '_non_overlapping.json'
                if (file_end_time > merged_end_time):
                    with open(output_file_name, 'w') as output_fp:
                        lines = open(fp, 'r+').readlines()
                        should_append = False
                        Logger.info('partially overlapping file: {}'.format(fp))
                        for line in lines:
                            if(should_append):
                                output_fp.write(line)
                            else:
                                cur_time = datetime.strptime(re.search('Timestamp":"(.*?)0Z', line).group(1), "%Y-%m-%dT%H:%M:%S.%f")
                                if (cur_time > merged_end_time):
                                    Logger.info('first non overlapping timestamp: {}'.format(cur_time))
                                    output_fp.write(line)
                                    should_append = True
                                    merge_remaining_files = True
                    output_fp.close()
                    merged_files.append(output_file_name)
                else:
                    Logger.info('duplicate file: {}'.format(fp))
        return CookedLogSequence(merged_files)