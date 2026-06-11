#!/usr/bin/env bash
html=$(curl -sL "https://app.comunaclic.cl/login")
echo "$html" | grep -o 'comunaclicGoogleClientId = "[^"]*"' | head -1
if echo "$html" | grep -q 'comunaclicGoogleClientId = ""'; then
  echo "WARNING: Google ClientId is EMPTY on production login page"
fi
