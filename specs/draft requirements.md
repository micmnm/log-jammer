Think of base ideas for an application that will help users monitor more pro-actively an application or platform.
The application will be plugged to logs or metrics and detect when something significant is happening:
* spike in number of errors
* particular metrics like duration of calls go up
* new errors (something not seen earlier)

Monitors ELK, Grafana or think of something else popular and detect based on logs/metrics.
Build a "library" of "known" errors so it will be able to tell if a particular error is new or a significant spike is happening.

Backend will be .net and front end more like a basic frontend which allows configurations but also a dashboard to view significant things going on but also to clasify errors.

Ask questions to clarify the needs.
Have in mind tools that are light and resource efficient. 
It will need to monitor 10k or more logs per minute at least and do not require remote LLM or GPU.