set -e

cd ~ardupilot

exec ./build/sitl/bin/arduplane \
  --model plane \
  --speedup 1 \
  --slave 0 \
  --serial0=udpclient:127.0.0.1:14550 \
  --sim-address=127.0.0.1 \
  --defaults ~/sitl-minimal.param \
  -I0
