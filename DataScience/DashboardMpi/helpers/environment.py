import os
from DashboardMpi.helpers import logger, runtime, pool
from DashboardMpi.helpers.input_provider import CachesProvider, LocalLogsProvider, ModelsProvider, PredictionsProvider


class Environment:
    def __init__(self, runtime_mode, procs, log_level, tmp_folder):
        rt = runtime.mpi() if runtime_mode == 'mpi' else runtime.local()

        self.runtime = rt
        self.job_pool = pool.multiproc_pool(procs) if procs > 1 else pool.seq_pool()
        self.logger = logger.console_logger(rt.get_node_id(), log_level)

        self.local_logs_provider = LocalLogsProvider(
            os.path.join(tmp_folder, 'logs')
        )
        self.caches_provider = CachesProvider(
            os.path.join(tmp_folder, 'caches')
        )
        self.models_provider = ModelsProvider(
            os.path.join(tmp_folder, 'models')
        )
        self.predictions_provider = PredictionsProvider(
            os.path.join(tmp_folder, 'predictions')
        )
