import abc

class LoggerBase(abc.ABC):

    @abc.abstractmethod
    def info(self, msg: str, tag='log', **kwargs):
        pass

    @abc.abstractmethod
    def warning(self, msg: str, tag='log', **kwargs):
        pass

    @abc.abstractmethod
    def error(self, msg: str, tag='exception', **kwargs):
        pass

    @abc.abstractmethod
    def exception(self, msg: str='', tag='exception', **kwargs):
        pass

    @abc.abstractmethod
    def close(self):
        pass