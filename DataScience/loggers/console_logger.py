import json
import sys
import traceback

class ConsoleLogger:

    def __log(self, msg, output=sys.stdout):
        print(msg, file=output)

    def info(self, msg: str, **kwargs):
        self.__log(msg)

    def warning(self, msg: str, **kwargs):
        self.__log(msg)

    def error(self, msg: str, **kwargs):
        self.__log(msg, sys.stderr)

    def exception(self, msg: str='', **kwargs):
        self.error("{0}: {1}".format(msg, traceback.format_exc()), **kwargs)

    def close(self):
        sys.stdout.close()
        sys.stderr.close()