try:
    from mpi4py import MPI
except Exception as e:
    print(e)
    print('MPI mode is not supported')


class local:
    def map(self, elements):
        return elements

    def reduce(self, elements):
        return elements

    def is_master(self):
        return True

    def get_node_id(self):
        return 0


class mpi:
    def map(self, elements):
        result = []
        i = self.get_node_id()
        step = MPI.COMM_WORLD.Get_size()
        while i < len(elements):
            result.append(elements[i])
            i = i + step
        return result

    def reduce(self, elements):
        return MPI.COMM_WORLD.allreduce(elements, MPI.SUM)

    def is_master(self):
        return MPI.COMM_WORLD.Get_rank() == 0

    def get_node_id(self):
        return MPI.COMM_WORLD.Get_rank()
