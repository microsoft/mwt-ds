#include "rapidjson/reader.h"
#include "rapidjson/writer.h"
#include "rapidjson/filereadstream.h"
#include "rapidjson/filewritestream.h"
#include "rapidjson/error/en.h"
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
#include <stdio.h>

using namespace rapidjson;
using namespace std;

struct EventReaderHandler : public rapidjson::BaseReaderHandler<rapidjson::UTF8<>, EventReaderHandler>
{
	bool _found_event_id_key = false;
	bool _found_model_id = false;

	string event_id;
	string model_id;

	bool Key(const char* str, SizeType len, bool copy) 
	{ 
		if (len == 8 && !strcmp(str, "_eventid"))
			_found_event_id_key = true;

		if (len == 8 && !strcmp(str, "_modelid"))
			_found_model_id = true;
		return true;
	}

	bool String(const char* str, SizeType len, bool copy) 
	{ 
		if (_found_event_id_key && event_id.length() == 0)
			event_id = string(str, str + len);

		if (_found_model_id && model_id.length() == 0)
			model_id = string(str, str + len);

		return Default();
	}

	bool Default() { return !(_found_event_id_key && _found_model_id); }
};

int main(int argc, char* argv[])
{
	if (argc != 2)
	{
		cerr << "Usage: " << argv[0] << " <file>";
		return -1;
	}

	ostringstream output_name;
	output_name << argv[1] << ".ids";

	ifstream input;
	input.open(argv[1]);
	ofstream output(output_name.str().c_str());

	string line;
	Reader reader;
	int line_number = 1;
	while (getline(input, line))
	{
		EventReaderHandler handler;
		InsituStringStream ss((char*)line.c_str());
		reader.Parse<kParseInsituFlag, InsituStringStream, EventReaderHandler>(ss, handler);

		if (handler.event_id.length() == 0)
		{
			cerr << "Missing event id on line " << line_number << endl;
			return -1;
		}
		output << handler.event_id << " " << handler.model_id << endl;

		line_number++;
	}
	input.close();
	output.close();
}