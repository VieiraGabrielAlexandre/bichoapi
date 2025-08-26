output "alb_dns_name"  { value = aws_lb.alb.dns_name }
output "rds_endpoint"  { value = aws_db_instance.this.address }
output "rds_db_name"   { value = aws_db_instance.this.db_name }
output "rds_master_username" { value = aws_db_instance.this.username }
output "note" { value = "Envie o artefato para s3://${var.app_s3_bucket}/${var.app_s3_key} antes do apply." }
