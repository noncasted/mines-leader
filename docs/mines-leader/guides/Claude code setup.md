docker run -d -p 4444:22 -v P:/noncasted/mines-leader/:/project --name mines-leader-ubuntu ubuntu

apt update
apt upgrade
apt-get install -y curl

curl -fsSL https://deb.nodesource.com/setup_current.x | sudo -E bash -
apt-get install -y nodejs

npm install -g @anthropic-ai/claude-code