{
  "logs": {
    "logs_collected": {
      "files": {
        "collect_list": [
          { "file_path": "/var/log/messages", "log_group_name": "/${project}/system", "log_stream_name": "{instance_id}-messages" },
          { "file_path": "/var/log/${project}/app.log", "log_group_name": "/${project}/app", "log_stream_name": "{instance_id}-app" }
        ]
      }
    }
  }
}
