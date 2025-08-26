#!/bin/bash
set -euo pipefail
PROJECT="${project}"
APP_DIR="/opt/${PROJECT}"
LOG_DIR="/var/log/${PROJECT}"
mkdir -p "${APP_DIR}" "${LOG_DIR}"

yum update -y
yum install -y unzip tar awscli amazon-cloudwatch-agent

aws s3 cp "s3://${app_s3_bucket}/${app_s3_key}" "/tmp/app.tar.gz"
tar -xzf /tmp/app.tar.gz -C "${APP_DIR}"

cat > "${APP_DIR}/.env" <<EOF
ASPNETCORE_URLS=http://0.0.0.0:80
ConnectionStrings__Db=Host=${db_host};Username=${db_user};Password=${db_pass};Database=${db_name};Pooling=true;Minimum Pool Size=1;Maximum Pool Size=50
BICHO_LOG_FILE=${LOG_DIR}/app.log
EOF

cat >/etc/systemd/system/${PROJECT}.service <<'EOF'
[Unit]
Description=Bicho API
After=network.target
[Service]
EnvironmentFile=/opt/bicho/.env
WorkingDirectory=/opt/bicho
ExecStart=/opt/bicho/BichoApi
Restart=always
RestartSec=3
StandardOutput=append:/var/log/bicho/app.log
StandardError=append:/var/log/bicho/app.log
[Install]
WantedBy=multi-user.target
EOF

if [ "${PROJECT}" != "bicho" ]; then
  sed -i "s#/opt/bicho#/opt/${PROJECT}#g" /etc/systemd/system/${PROJECT}.service
  sed -i "s#/var/log/bicho#/var/log/${PROJECT}#g" /etc/systemd/system/${PROJECT}.service
  sed -i "s#EnvironmentFile=/opt/${PROJECT}/.env#EnvironmentFile=/opt/${PROJECT}/.env#g" /etc/systemd/system/${PROJECT}.service
fi

systemctl daemon-reload
systemctl enable ${PROJECT}.service
systemctl start ${PROJECT}.service

echo "${cwagent_json}" | base64 -d > /opt/aws/amazon-cloudwatch-agent.json
/opt/aws/amazon-cloudwatch-agent/bin/amazon-cloudwatch-agent-ctl \
  -a fetch-config -m ec2 -c file:/opt/aws/amazon-cloudwatch-agent.json -s
