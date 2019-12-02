import ffmpeg from './ffmpeg/ffmpeg';
import youtubedl from './youtubedl/youtubedl';
import { ipcMain } from 'electron';

ipcMain.on('abort', (event, args) => {
  ffmpeg.abort()
    .then(() => {
      return youtubedl.abort();
    })
    .then(() => {
      event.reply('client/abort');
    });
});

ipcMain.on('ffmpeg/combine', (event, args) => {
  ffmpeg.combine(args)
    .then((result) => {
      event.reply('client/ffmpeg/combine', { result });
    }).catch((error) => {
      event.reply('client/ffmpeg/combine', { error });
    }); ;
});

ipcMain.on('ffmpeg/convert', (event, args) => {
  ffmpeg.convert(args)
    .then((result) => {
      event.reply('client/ffmpeg/convert', { result });
    }).catch((error) => {
      event.reply('client/ffmpeg/convert', { error });
    }); ;
});

ipcMain.on('youtubedl/get_info', (event, args) => {
  youtubedl.getInfo(args)
    .then((result) => {
      event.reply('client/youtubedl/get_info', { result });
    }).catch((error) => {
      event.reply('client/youtubedl/get_info', { error });
    }); ;
});

ipcMain.on('youtubedl/start_video_stream', (event, args) => {
  youtubedl.streamVideoIntoFile(args)
    .then((result) => {
      event.reply('client/youtubedl/start_video_stream', { result });
    }).catch((error) => {
      event.reply('client/youtubedl/start_video_stream', { error });
    }); ;
});
