import argparse
import datetime
import os
from azure.storage.blob import BlockBlobService
from helpers import vw, logger, environment, runtime, path_generator, input_provider, pool, preprocessing, grid, sweep, command, dashboard


def dashboard_e2e(app_container, connection_string, app_folder, vw_path,
                  start, end, tmp_folder, env, procs, log_level,
                  output_connection_string, output_container, output_path):
    cache_folder = os.path.join(tmp_folder, 'cache')
    model_folder = os.path.join(tmp_folder, 'model')
    pred_folder = os.path.join(tmp_folder, 'pred')
    logs_folder = os.path.join(tmp_folder, 'logs')
    rt = runtime.mpi() if env == 'mpi' else runtime.local()
    lg = logger.console_logger(rt.get_node_id(), log_level)

    env = environment.environment(
        vw_path=vw_path,
        runtime=rt,
        job_pool=pool.multiproc_pool(procs) if procs > 1 else pool.seq_pool(),
        txt_provider=input_provider.azure_logs_provider(
            app_container,
            connection_string,
            app_folder,
            start,
            end,
            logs_folder,
            lg
        ),
        cache_path_gen=path_generator.cache_path_generator(cache_folder),
        pred_path_gen=path_generator.pred_path_generator(pred_folder),
        model_path_gen=path_generator.model_path_generator(model_folder),
        cache_provider=input_provider.cache_provider(cache_folder),
        logger=lg
    )

    base_command = {'#base': '--cb_adf --dsjson --save_resume --preserve_performance_counters'}
    vw.cache(base_command, env)

    namespaces = preprocessing.extract_namespaces(
        open(env.txt_provider.get()[0], 'r', encoding='utf-8')
    )

    marginals_grid = preprocessing.get_marginals_grid(
        '#marginals', namespaces[2]
    )

    interactions_grid = preprocessing.get_interactions_grid(
        '#interactions', namespaces[0], namespaces[1]
    )

    multi_grid = grid.generate(interactions_grid, marginals_grid)

    best = sweep.sweep(multi_grid, env, base_command)

    predict_opts = {'#base': '--cb_explore_adf --epsilon 0.2 --dsjson --save_resume --preserve_performance_counters'}
    commands = dict(map(
        lambda lo: (lo[0], command.apply(lo[1], predict_opts)), best.items()
    ))
    vw.predict(commands, env)

    local_dashboard_path = os.path.join(tmp_folder, 'dashboard.json')
    dashboard.create(commands, local_dashboard_path, env)
    if env.runtime.is_master() and output_connection_string:
        bbs = BlockBlobService(connection_string=output_connection_string)
        lg.info(output_container + ':' + output_path + ': Uploading from ' + local_dashboard_path)
        bbs.create_blob_from_path(output_container, output_path, local_dashboard_path, max_connections=4)
        lg.info(output_container + ':' + output_path + ': Succeesfully uploaded')


def main():
    parser = argparse.ArgumentParser("dashboard e2e")
    parser.add_argument("--app_folder", type=str, help="app folder")
    parser.add_argument("--vw", type=str, help="vw path")
    parser.add_argument("--start_date", type=str, help="start date")
    parser.add_argument("--end_date", type=str, help="end date")
    parser.add_argument("--tmp_folder", type=str, help="temporary folder")
    parser.add_argument("--app_container", type=str, help="app_container")
    parser.add_argument("--connection_string", type=str, help="connection_string")
    parser.add_argument("--procs", type=int, help="procs")
    parser.add_argument("--env", type=str, help="environment (local / mpi)", default="local")
    parser.add_argument("--log_level", type=str, help="log level (CRITICAL / ERROR / WARNING / INFO / DEBUG)",
                        default='INFO')
    parser.add_argument("--output_connection_string", type=str, help="output connection_string")
    parser.add_argument("--output_container", type=str, help="output_container")
    parser.add_argument("--output_path", type=str, help="dashboard file's path inside output container")

    args = parser.parse_args()

    date_format = '%m/%d/%Y'

    os.makedirs(args.tmp_folder, exist_ok=True)

    start = datetime.datetime.strptime(args.start_date, date_format)
    end = datetime.datetime.strptime(args.end_date, date_format)

    dashboard_e2e(args.app_container, args.connection_string, args.app_folder,
                  args.vw, start, end, args.tmp_folder, args.env, args.procs,
                  args.log_level, args.output_connection_string,
                  args.output_container, args.output_path)


if __name__ == '__main__':
    main()
