#!/bin/bash
magick mogrify -path . -format ico -density 600 -define icon:auto-resize=256,128,64,48,40,32,24,16 -background none netczicompress.svg