import logging
import time


# def console_logger(node_id, level='INFO'):
#    logger = logging.getLogger('dashboard_logger')
#    stream_handler = logging.StreamHandler(sys.stdout)
#    formatter = logging.Formatter('[%(node_id)s][%(asctime)s]: %(message)s')
#    stream_handler.setFormatter(formatter)
#    logger.setLevel(logging.getLevelName(level))
#    logger.addHandler(stream_handler)
#    extra = {'node_id': str(node_id)}
#    return logging.LoggerAdapter(logger, extra)

# workaround for logger pickling

class console_logger:
    def __init__(self, node_id, level='INFO'):
        self.node_id = node_id
        self.level = logging.getLevelName(level)

    def debug(self, message):
        if self.level <= logging.DEBUG: self._trace(message)

    def info(self, message):
        if self.level <= logging.INFO: self._trace(message)

    def warning(self, message):
        if self.level <= logging.WARNING: self._trace(message)

    def error(self, message):
        if self.level <= logging.ERROR: self._trace(message)

    def critical(self, message):
        if self.level <= logging.CRITICAL: self._trace(message)

    def _trace(self, message):
        prefix = '[' + str(self.node_id) + '][' + time.strftime("%d-%m-%Y %H:%M:%S", time.localtime(time.time())) + ']'
        print(prefix + message)
