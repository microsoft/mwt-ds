import multiprocessing


class seq_pool:
    def map(self, task, inputs):
        result = []
        for i in inputs:
            result.append(task(i))
        return result


class multiproc_pool:
    def __init__(self, procs):
        self.procs = procs

    def map(self, task, inputs):
        p = multiprocessing.Pool(self.procs)
        result = p.map(task, inputs)
        p.close()
        p.join()
        return result
