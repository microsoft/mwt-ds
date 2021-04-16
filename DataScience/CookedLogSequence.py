from datetime import datetime
from loggers import Logger
import re, os

class CookedLogSequence:
    timestamp_format = '%Y-%m-%dT%H:%M:%S.%f'
    timestamp_pattern = r'[^\\]"Timestamp"\s*:\s*"(.*?)0Z"'
    def __init__(self, files):
        self.files = files
    
    def get_time_from_log_line(self, log_line):
        return datetime.strptime(re.search(self.timestamp_pattern, log_line).group(1), self.timestamp_format)

    def get_file_start_time(self, fp):
        with open(fp, 'r') as f:
            first_line = f.readline()
            return self.get_time_from_log_line(first_line)
    
    def get_file_end_time(self, fp):

        with open(fp, 'r') as f:
            last_line = f.readlines()[-1]
            return self.get_time_from_log_line(last_line)
    
    def get_first_ts(self):
        if len(self.files) > 0:
            return self.get_file_start_time(self.files[0])
        return datetime.min
    
    def get_last_ts(self):
        if len(self.files) > 0:
            return self.get_file_end_time(self.files[-1])
        return datetime.min
    
    def find_first_file_to_merge(self, files, merged_end_time):
        if self.get_file_end_time(files[-1]) < merged_end_time:
            return -1
        candidate_index = len(files) - 1
        for i, fp in enumerate(files):
            if self.get_file_start_time(fp) > merged_end_time:
                candidate_index = i
                break
        if candidate_index > 0 and self.get_file_end_time(files[candidate_index - 1]) > merged_end_time:
            return candidate_index - 1
        return candidate_index

    def find_first_line_to_merge(self, fp, merged_end_time):
        lines = open(fp, 'r').readlines()
        for i, line in enumerate(lines):
            line_time = self.get_time_from_log_line(line)
            if line_time > merged_end_time:
                return i
        return -1

    #Merging of CookedLogSequence must be in sequential order
    #Ex. We have three sequences in the following order A B C
    #if we merge(A,C), B cannot later be merged
    #we have to in sequential order merge(merge(A,B),C). 
    def merge(self, other):
        merged_files = list(self.files)
        merged_end_time = self.get_last_ts()
        overlapping_file_index = self.find_first_file_to_merge(other.files, merged_end_time)
        if overlapping_file_index > -1:
            merge_fp = other.files[overlapping_file_index]
            first_line_to_append = self.find_first_line_to_merge(merge_fp, merged_end_time)
            if(first_line_to_append == 0):
                merged_files.extend(other.files[overlapping_file_index:])
            else:
                Logger.info('partially overlapping file: {}'.format(merge_fp))
                output_fp = os.path.splitext(merge_fp)[0] + '_non_overlapping.json'
                with open(output_fp, 'w') as output_file:
                    lines = open(merge_fp, 'r').readlines()
                    output_file.writelines(lines[first_line_to_append:])
                output_file.close()
                merged_files.append(output_fp)
                merged_files.extend(other.files[overlapping_file_index + 1:])
        else:
            Logger.info('duplicate files: {}'.format(other.files))

        return CookedLogSequence(merged_files)
