import os
import sys
sys.path.append("..")
import dashboard_utils

def create(path, env, commands, enable_sweep):
    d = {}
    for log_path in env.txt_provider.get():
        prediction_path_list = []
        if enable_sweep:
            predictions_dir = env.pred_path_gen.get_folder(
                env.cache_path_gen.get(log_path)
            )

            predictions = os.listdir(predictions_dir)
            for prediction_file in predictions:
                prediction_path = os.path.join(predictions_dir, prediction_file)
                prediction_path_list.append(prediction_path)
        d = dashboard_utils.create_stats(log_path, d, prediction_path_list)
    dashboard_utils.output_dashboard_data(d, path, commands)
