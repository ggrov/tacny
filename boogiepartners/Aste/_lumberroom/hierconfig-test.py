from config import Config

with file('../main-hcfg.cfg') as f:
    cfg = Config(f)

for k in cfg.Paths:
    print k, cfg.Paths[k]

print cfg.Paths.Boogie, type(cfg.Paths.Boogie)
print cfg.SVN.Update, type(cfg.SVN.Update)
print cfg.Logging.Backups, type(cfg.Logging.Backups)

# print "%s" % cfg.Paths

# for m in cfg.messages:
    # s = '%s, %s' % (m.message, m.name)
    # try:
        # print >> m.stream, s
    # except IOError, e:
        # print e
