# ==========================================================================================
# Parse out the vw audit logs to a dataframe.
# ==========================================================================================

import numpy as np
import pandas as pd
import re
import subprocess as shell
import os
import argparse


# CONSTANTS
VW_AUDIT_COMMAND = "vw -t -i {model} --dsjson {test_input} -p {test_output} -l 0.001 --audit"

def run_vw_audit_command(model_file, input_vector, debug=False):
    """
    Run the VW audit command for a model and input vectors.

    @param: model_file The input model file.
    @param: input_vector: The input vector file.
    @param: debug: The debug flag for advanced printing.

    @returns: The vw audit command capture from command line.
    """
    # Get prediction path from input vector.
    output_file = os.path.split(input_vector)[-1] +  '.pred'

    vw_audit_cmd = VW_AUDIT_COMMAND.format(model=model_file,
        test_input=input_vector,
        test_output=output_file)

    if (debug):
        print("VW Audit command: {0}".format(vw_audit_cmd))
    
    # Run the command in windows and check output.
    vw_cmd_output = shell.check_output(vw_audit_cmd, shell=True).decode()    

    if (debug):
        print(vw_cmd_output)

    return vw_cmd_output

def parse_audit_command(vw_cmd_output, audit_output_file, debug=False):
    """
    Parse the output of the audit command and output to a dataframe.

    @param: vw_cmd_output The output of the audit command.
    @param: audit_output_file The file to which pandas data frame will be written.

    @returns: The dataframe containing the audit command output.
    """
    coeff_list = []
    line_number = 0
    for line in [s.strip() for s in vw_cmd_output.splitlines()]:
        line_number += 1
        print("Line {0}: {1}".format(line_number, line))

        # Process lines that have more than one tokens.
        coefficients = line.split("\t")
        print("Num coefficients: {0}".format(len(coefficients)))

        if len(coefficients) <= 1:
            # This line does not contain coefficients
            continue

        # process coefficients
        for coeff in coefficients:
            coeff_term = coeff.split(":")
            if (debug):
                print("Coefficients: {0}", coeff_term)

            coeff_list.append({"term1": coeff_term[0],
                "term2": coeff_term[1],
                "term3": coeff_term[2],
                "value": coeff_term[3]})

    # Convert coefficients to pandas and output
    df_coeff = pd.DataFrame(coeff_list)

    # De-duplicate
    df_coeff.set_index(["term1", "term2", "term3"], inplace=True)
    df_coeff = df_coeff[~df_coeff.index.duplicated(keep='first')]

    df_coeff.to_csv(audit_output_file, sep='\t', index=True)
    return df_coeff

def pprint_vw_audit(model_file, input_vector, audit_output_file, debug):
    """
    Run the VW audit command for a model and input vectors.

    @param: model_file The input model file.
    @param: input_vector: The input vector file.
    @param: audit_output_file The file to which pandas data frame will be written.
    @param: debug: The debug flag for advanced printing.
    """
    vw_cmd_output = run_vw_audit_command(model_file, input_vector, debug)
    parse_audit_command(vw_cmd_output, audit_output_file)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()

    parser.add_argument('-m', '--model',        type=str, required=True)
    parser.add_argument('-i', '--input',        type=str, required=True)
    parser.add_argument('-o', '--output',       type=str, required=True)
    parser.add_argument('-d', '--debug',        type=str, required=False, default=True)

    args = parser.parse_args()
    print(args)

    # Pretty print the VW audit.
    pprint_vw_audit(args.model, args.input, args.output, args.debug)
