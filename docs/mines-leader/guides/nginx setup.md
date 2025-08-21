
Reset links to sites-enabled
sudo rm -f /etc/nginx/sites-enabled/*
ls -la /etc/nginx/sites-available/

sudo ln -s /etc/nginx/sites-available/gateway.minesleader.xyz /etc/nginx/sites-enabled/ 
sudo ln -s /etc/nginx/sites-available/console.minesleader.xyz /etc/nginx/sites-enabled/ 
sudo ln -s /etc/nginx/sites-available/server.minesleader.xyz /etc/nginx/sites-enabled/ 
sudo ln -s /etc/nginx/sites-available/aspire.minesleader.xyz /etc/nginx/sites-enabled/

sudo systemctl restart nginx
sudo systemctl status nginx

sudo certbot certonly --manual --preferred-challenges dns   -d gateway.minesleader.xyz   -v
sudo certbot certonly --manual --preferred-challenges dns   -d server.minesleader.xyz   -v
sudo certbot certonly --manual --preferred-challenges dns   -d console.minesleader.xyz   -v
sudo certbot certonly --manual --preferred-challenges dns   -d aspire.minesleader.xyz   -v



Dotnet&aspire setup

apt update
apt upgrade

apt install -y wget apt-transport-https software-properties-common
apt install git tmux -y
apt install curl -y
apt install git tmux curl -y

wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt update
sudo apt install -y dotnet-sdk-9.0
export PATH="$HOME/.dotnet:$PATH"

curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --channel 9.0
export PATH="$PATH:$HOME/.dotnet"

curl -sSL https://aspire.dev/install.sh | bash

IF NOT EXECUTED
chmod +x aspire-install.sh
./aspire-install.sh

dotnet workload install aspire

source /root/.bashrc 

mkdir mslead
cd mslead
git clone https://github.com/noncasted/mines-leader.git

cd /mslead/mines-leader/backend/Aspire/AppHost/
tmux

aspire run


dotnet dev-certs https --clean
dotnet dev-certs https --clean dotnet dev-certs https --trust
mkdir -p /https
chmod 755 /https
dotnet dev-certs https -ep /https/aspnetapp.pfx -p root


dotnet clean --configuration Debug 
dotnet clean --configuration Release 
# Remove bin and obj folders manually if needed 
find . -name "bin" -type d -exec rm -rf {} + 2>/dev/null find . -name "obj" -type d -exec rm -rf {} + 2>/dev/null


apt-get update && apt-get install -y socat
socat TCP-LISTEN:17217,bind=0.0.0.0,fork TCP:10.0.1.23:17216 &

netstat -tlnp | grep 17216

apt-get update && apt-get install -y nginx
server { listen 0.0.0.0:17217; server_name _; location / { proxy_pass https://10.0.1.23:17216; proxy_set_header Host $host; proxy_set_header X-Real-IP $remote_addr; proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for; proxy_set_header X-Forwarded-Proto $scheme; # Handle SSL verification issues with backend proxy_ssl_verify off; proxy_ssl_session_reuse on; } }