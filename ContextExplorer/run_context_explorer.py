import sys
from context_explorer import *

def run_context_explorer(config_path):
    ce = ContextExplorer_DSJson(config_path)
    exp_data = ce.generate_report()

if __name__ == "__main__":
    # Pass the path to the config file to run Context Explorer
    run_context_explorer(sys.argv[1])
