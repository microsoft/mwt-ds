from fluent import asyncsender as sender
import json
import sys
import traceback

class Logger:
    app_id = ""
    job_id = ""

    @staticmethod
    def create_logger(app_id, job_id):
        sender.setup('microsoft.cloudai.personalization', host='localhost', port=24224)
        Logger.app_id = app_id
        Logger.job_id = job_id

    @staticmethod
    def __log(level, msg, output=sys.stdout, tag='log', **kwargs):
        try:
            print(msg, file=output, flush=True)
            base_log = {'level': level, 'message': msg, 'appId': Logger.app_id, 'jobId': Logger.job_id}
            log_content = {**base_log, **kwargs}
            logger = sender.get_global_sender()
            if not logger.emit(tag, log_content):
                print(logger.last_error, flush=True)
                logger.clear_last_error()
        except Exception as e:
            print("Error while logging: {}".format(e), flush=True)

    @staticmethod
    def info(msg: str, **kwargs):
        Logger.__log('INFO', msg, **kwargs)

    @staticmethod
    def warning(msg: str, **kwargs):
        Logger.__log('WARNING', msg, **kwargs)

    @staticmethod
    def error(msg: str, **kwargs):
        Logger.__log('ERROR', msg, sys.stderr, 'exception', **kwargs)

    @staticmethod
    def exception(msg: str='', **kwargs):
        Logger.error(msg, exception=traceback.format_exc(), **kwargs)

    @staticmethod
    def close():
        logger = sender.get_global_sender()
        logger.close()