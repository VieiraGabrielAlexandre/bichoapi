variable "project"           { type = string  default = "bicho" }
variable "env"               { type = string  default = "prod" }
variable "aws_region"        { type = string  default = "us-east-1" }
variable "vpc_cidr"          { type = string  default = "10.0.0.0/16" }
variable "public_subnet_cidrs" { type = list(string) default = ["10.0.1.0/24","10.0.2.0/24"] }
variable "private_db_subnet_cidrs" { type = list(string) default = ["10.0.101.0/24","10.0.102.0/24"] }
variable "azs" { type = list(string) default = ["us-east-1a","us-east-1b"] }
variable "instance_type"     { type = string  default = "t3.small" }
variable "app_s3_bucket"     { type = string }
variable "app_s3_key"        { type = string }
variable "db_engine_version" { type = string  default = "16.3" }
variable "db_username"       { type = string  default = "appuser" }
variable "db_name"           { type = string  default = "bicho" }
variable "allowed_ssh_cidr"  { type = string  default = "" }
