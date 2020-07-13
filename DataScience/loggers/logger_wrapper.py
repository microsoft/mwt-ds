from fluent import asyncsender as sender
import json
import sys
import traceback
from loggers.geneva_logger import GenevaLogger 
from loggers.console_logger import ConsoleLogger

class Logger:
    loggers = []

    @classmethod
    def create_loggers(cls, geneva=False, **geneva_args):
        Logger.loggers.append(ConsoleLogger())
        if geneva:
            Logger.loggers.append(GenevaLogger(**geneva_args))

    @classmethod
    def info(cls, msg: str, **kwargs):
        for logger in Logger.loggers:
            logger.info(msg, **kwargs)

    @classmethod
    def warning(cls, msg: str, **kwargs):
        for logger in Logger.loggers:
            logger.warning(msg, **kwargs)

    @classmethod
    def error(cls, msg: str, **kwargs):
        for logger in Logger.loggers:
            logger.error(msg, **kwargs)

    @classmethod
    def exception(cls, msg: str='', **kwargs):
        for logger in Logger.loggers:
            logger.exception(msg, **kwargs)

    @classmethod
    def close(cls):
        for logger in Logger.loggers:
            logger.close()

