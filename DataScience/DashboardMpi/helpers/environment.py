class environment:
    def __init__(self, vw_path, runtime, job_pool, cache_path_gen=None,
                 model_path_gen=None, pred_path_gen=None, cache_provider=None,
                 txt_provider=None, logger=None):
        self.vw_path = vw_path
        self.runtime = runtime
        self.job_pool = job_pool
        self.logger = logger
        self.cache_path_gen = cache_path_gen
        self.model_path_gen = model_path_gen
        self.pred_path_gen = pred_path_gen
        self.cache_provider = cache_provider
        self.txt_provider = txt_provider
