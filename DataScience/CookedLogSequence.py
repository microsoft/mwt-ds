from datetime import datetime
from loggers import Logger
import re, os

class CookedLogSequence:
    timestamp_format = '%Y-%m-%dT%H:%M:%S.%f'
    timestamp_pattern = r'[^\\]"Timestamp"\s*:\s*"(.*?)0Z"'
    def __init__(self, files):
        self.files = files
    
    @staticmethod
    def __get_time_from_log_line(log_line):
        return datetime.strptime(re.search(CookedLogSequence.timestamp_pattern, log_line).group(1), CookedLogSequence.timestamp_format)

    @staticmethod
    def __get_file_start_time(fp):
        with open(fp, 'r') as f:
            first_line = f.readline()
            return CookedLogSequence.__get_time_from_log_line(first_line)
    
    @staticmethod
    def __get_file_end_time(fp):
        with open(fp, 'r') as f:
            last_line = f.readlines()[-1]
            return CookedLogSequence.__get_time_from_log_line(last_line)
    
    def get_first_ts(self):
        if len(self.files) > 0:
            return CookedLogSequence.__get_file_start_time(self.files[0])
        return datetime.min
    
    def get_last_ts(self):
        if len(self.files) > 0:
            return CookedLogSequence.__get_file_end_time(self.files[-1])
        return datetime.min
    
    @staticmethod
    def __find_first_file_to_merge(files, merged_end_time):
        #optimization -- if the last file ends before merge_end_time then entire list of files is duplicate
        if CookedLogSequence.__get_file_end_time(files[-1]) < merged_end_time:
            return None
        candidate_index = len(files) - 1
        for i, fp in enumerate(files):
            if CookedLogSequence.__get_file_start_time(fp) > merged_end_time:
                #first file that has no overlap
                candidate_index = i
                break
        #check if the file before candidate file has an averlap with the merged files (ie starts before merged_end_time and ends after)
        if candidate_index > 0 and CookedLogSequence.__get_file_end_time(files[candidate_index - 1]) > merged_end_time:
            return candidate_index - 1
        return candidate_index

    @staticmethod
    def __find_first_line_to_merge(lines, merged_end_time):
        for i, line in enumerate(lines):
            line_time = CookedLogSequence.__get_time_from_log_line(line)
            if line_time > merged_end_time:
                return i
        return None

    #Merging of CookedLogSequence must be in sequential order
    #Ex. We have three sequences in the following order A B C
    #if we merge(A,C), B cannot later be merged
    #we have to in sequential order merge(merge(A,B),C). 
    def merge(self, other):
        merged_files = list(self.files)
        merged_end_time = self.get_last_ts()
        overlapping_file_index = CookedLogSequence.__find_first_file_to_merge(other.files, merged_end_time)
        if overlapping_file_index != None:
            merge_fp = other.files[overlapping_file_index]
            with open(merge_fp, 'r') as merge:
                lines = merge.readlines()
            first_line_to_append = CookedLogSequence.__find_first_line_to_merge(lines, merged_end_time)
            if first_line_to_append == 0:
                #merge entire file
                merged_files.append(merge_fp)
            elif first_line_to_append != None:
                Logger.info('partially overlapping file: {}'.format(merge_fp))
                output_fp = os.path.splitext(merge_fp)[0] + '_non_overlapping.json'
                #create new file starting at the first_line_to_append to the end of the file.
                with open(output_fp, 'w') as output_file:
                    output_file.writelines(lines[first_line_to_append:])
                merged_files.append(output_fp)
            merged_files.extend(other.files[overlapping_file_index + 1:])
        else:
            Logger.info('duplicate files: {}'.format(other.files))

        return CookedLogSequence(merged_files)
