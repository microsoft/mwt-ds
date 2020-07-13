from fluent import asyncsender as sender
import json
import sys
import traceback
from loggers.geneva_logger import GenevaLogger 
from loggers.console_logger import ConsoleLogger

class Logger:
    loggers = []

    @classmethod
    def create_loggers(cls, **kwargs):
        cls.loggers.append(ConsoleLogger())
        if "geneva_namespace" in kwargs:
            cls.loggers.append(GenevaLogger(namespace=kwargs["geneva_namespace"],
                                            host=kwargs["geneva_host"],
                                            port=kwargs["geneva_port"],
                                            **kwargs["geneva_gbl_vals"]))

    @classmethod
    def info(cls, msg: str, **kwargs):
        for logger in cls.loggers:
            logger.info(msg, **kwargs)

    @classmethod
    def warning(cls, msg: str, **kwargs):
        for logger in cls.loggers:
            logger.warning(msg, **kwargs)

    @classmethod
    def error(cls, msg: str, **kwargs):
        for logger in cls.loggers:
            logger.error(msg, **kwargs)

    @classmethod
    def exception(cls, msg: str='', **kwargs):
        for logger in cls.loggers:
            logger.exception(msg, **kwargs)

    @classmethod
    def close(cls):
        for logger in cls.loggers:
            logger.close()

